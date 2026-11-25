using System.Collections.Generic;
using UnityEngine;
using Species = FacilityEncounter.Species;

public sealed class FacilityCreature : MonoBehaviour
{
    private enum State { Patrol, Windup, Charge, Stunned, Evade, Discharge, Drain, Perch, Drop, Flee, PounceWindup, Pounce, Recover, Hunt, Dead, Roar, MeleeWindup, Melee }

    private FacilityEncounter encounter;
    private Species species;
    private State state;
    private FacilityCreatureSkin skin;
    private CapsuleCollider body;
    private Rigidbody motor;
    private AudioSource audioSource;
    private Vector3 chargeDirection, spawn, wanderTarget, lastKnownPlayer, dischargeTarget;
    private float health, maximum, radius, height, deadline, nextAttack, nextPath, nextCall, nextEscape, walkPhase, lastHit, alertUntil, retreatUntil;
    private int patrolIndex;
    private Vector3 pounceOrigin;
    private bool bossAwake, bossEnrageShown, bossAttackHit;
    private float bossStateStarted, bossStuckTime, bossNextPropHit, bossNextFootstep, bossNextPain;
    private Collider bossLastProp;
    private int bossAvoidSide = 1;
    internal bool IsEnraged => species == Species.Goliath && HealthFraction <= .5f;
    internal bool BossAwake => bossAwake;
    internal float CurrentHealth => health;
    internal float MaximumHealth => maximum;
    internal string BossIntent => state switch
    {
        State.Roar => IsEnraged ? "ENRAGED / FASTER ATTACKS" : "TARGET ACQUIRED",
        State.Windup => "CHARGE INCOMING / DODGE SIDEWAYS",
        State.Charge => "CHARGING",
        State.MeleeWindup => "HEAVY STRIKE / GET BACK",
        State.Melee => "HEAVY STRIKE",
        State.Stunned => "STUNNED / OPEN FIRE",
        State.Recover => "RECOVERING",
        _ => IsEnraged ? "ENRAGED / HUNTING" : "HUNTING"
    };
    private readonly List<Vector3> path = new();
    private readonly RaycastHit[] sweep = new RaycastHit[32];

    internal bool Alive => state != State.Dead;
    internal float HealthFraction => health / maximum;
    internal bool Hidden => species == Species.Stalker && state == State.Perch && !encounter.Powered && Time.time - lastHit > 1.4f;
    internal Vector3 AimPoint => transform.position + Vector3.up * (height * .66f);

    internal void Setup(FacilityEncounter owner, Species kind)
    {
        encounter = owner;
        species = kind;
        spawn = transform.position;
        (radius, height, maximum, state) = kind switch
        {
            Species.Goliath => (.72f, 2.9f, 600f, State.Patrol),
            Species.Leech => (.45f, .86f, 110f, State.Evade),
            Species.Stalker => (.55f, .94f, 155f, State.Perch),
            _ => (.23f, .40f, 44f, State.Hunt)
        };
        maximum *= 1 + (owner.Difficulty - 1) * .7f;
        health = maximum;
        lastHit = -10;
        motor = gameObject.AddComponent<Rigidbody>();
        motor.isKinematic = true;
        motor.useGravity = false;
        body = gameObject.AddComponent<CapsuleCollider>();
        body.radius = radius;
        body.height = height;
        body.center = Vector3.up * height * .5f;
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 1;
        audioSource.minDistance = 2.5f;
        audioSource.maxDistance = kind == Species.Goliath ? 34 : 24;
        skin = new FacilityCreatureSkin(transform, kind, owner.Effects);
        nextCall = Time.time + Random.Range(1f, 4f);
    }

    internal void InitializePath()
    {
        if (species == Species.Goliath)
        {
            // Cargo may have moved since the encounter was saved. Choose a free
            // starting position before enabling movement rather than spawning in a rack.
            if (Walkable(transform.position)) return;
            for (int ring = 1; ring <= 5; ring++)
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8;
                Vector3 alternate = spawn + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (ring * .7f);
                if (!Within(alternate) || !Walkable(alternate)) continue;
                spawn = transform.position = alternate;
                return;
            }
            Debug.LogWarning("Goliath has no clear spawn space. Check the warehouse cargo layout.", this);
            return;
        }
        if (state != State.Perch && Physics.CheckSphere(transform.position + Vector3.up * .16f, .1f, ~0, QueryTriggerInteraction.Ignore))
        {
            // The check includes this creature, so only correct placements blocked by world geometry.
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                if (Walkable(spawn)) break;
                Vector3 alternate = spawn + offset * 1.4f;
                if (Walkable(alternate)) transform.position = alternate;
            }
        }
    }

    internal bool VisibleFrom(Vector3 origin)
    {
        Vector3 offset = AimPoint - origin;
        int count = Physics.RaycastNonAlloc(origin, offset.normalized, sweep, offset.magnitude - .08f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider shape = sweep[i].collider;
            if (shape != body && !shape.GetComponentInParent<PlayerMovement>())
                return false;
        }
        return true;
    }

    internal void TakeHit(float damage)
    {
        if (!Alive) return;
        health = Mathf.Max(0, health - damage);
        lastHit = Time.time;
        if (species == Species.Goliath)
        {
            lastKnownPlayer = encounter.Player;
            alertUntil = Time.time + 12f;
            nextPath = 0;
        }
        if (species != Species.Goliath) Call(.72f);
        else if (Time.time >= bossNextPain)
        {
            bossNextPain = Time.time + .75f;
            Call(.45f);
        }
        if (health > 0)
        {
            if (state == State.Perch) StartDrop();
            return;
        }
        state = State.Dead;
        deadline = Time.time;
        body.enabled = false;
    }

    private bool Looking(float dot)
    {
        Transform eye = encounter.View.transform;
        return Vector3.Dot(eye.forward, (AimPoint - eye.position).normalized) >= dot && VisibleFrom(eye.position);
    }

    private void Update()
    {
        if (!Alive)
        {
            float collapse = Mathf.Clamp01((Time.time - deadline) * 1.8f);
            if (species == Species.Goliath)
                skin.AnimateBrute(walkPhase, 0, FacilityCreatureSkin.BruteAction.Idle, 0, 0, collapse, IsEnraged);
            else skin.Animate(walkPhase, 0, 0, 0, collapse, false, false);
            return;
        }
        if (!encounter.PlayerAlive) return;
        if (Time.time >= nextCall && Vector3.Distance(transform.position, encounter.Player) < 21)
        {
            Call(species == Species.Swarmer ? .23f : .36f);
            nextCall = Time.time + Random.Range(4f, 8f);
        }

        Vector3 previous = transform.position;
        switch (species)
        {
            case Species.Goliath: Goliath(); break;
            case Species.Leech: Leech(); break;
            case Species.Stalker: Stalker(); break;
            case Species.Swarmer: Swarmer(); break;
        }
        float speed = Vector3.Distance(previous, transform.position) / Mathf.Max(.001f, Time.deltaTime);
        walkPhase += Time.deltaTime * Mathf.Lerp(2, 14, Mathf.Clamp01(speed / 5));
        float windup = state == State.Windup || state == State.Discharge || state == State.PounceWindup ? 1 : 0;
        if (species == Species.Goliath)
        {
            FacilityCreatureSkin.BruteAction action = state switch
            {
                State.Roar => FacilityCreatureSkin.BruteAction.Roar,
                State.Windup => FacilityCreatureSkin.BruteAction.ChargeWindup,
                State.Charge => FacilityCreatureSkin.BruteAction.Charge,
                State.MeleeWindup => FacilityCreatureSkin.BruteAction.MeleeWindup,
                State.Melee => FacilityCreatureSkin.BruteAction.Melee,
                State.Stunned => FacilityCreatureSkin.BruteAction.Stunned,
                State.Recover => FacilityCreatureSkin.BruteAction.Recover,
                _ => FacilityCreatureSkin.BruteAction.Idle
            };
            float progress = Mathf.InverseLerp(bossStateStarted, deadline, Time.time);
            skin.AnimateBrute(walkPhase, Mathf.Clamp01(speed / 3), action, progress,
                Mathf.Clamp01(1 - (Time.time - lastHit) * 6), 0, IsEnraged);
            if (speed > .6f && Time.time >= bossNextFootstep)
            {
                bossNextFootstep = Time.time + (state == State.Charge ? .22f : .48f);
                audioSource.pitch = .66f;
                audioSource.PlayOneShot(encounter.Effects.Footstep(FacilityEncounter.StepSurface.Concrete), .65f);
            }
        }
        else
            skin.Animate(walkPhase, Mathf.Clamp01(speed / 2), windup,
                Mathf.Clamp01(1 - (Time.time - lastHit) * 5), 0, Hidden, state == State.Perch);
    }

    private void BeginBossState(State next, float duration)
    {
        state = next;
        bossStateStarted = Time.time;
        deadline = Time.time + duration;
        bossAttackHit = false;
    }

    private void Goliath()
    {
        Vector3 toward = Flat(encounter.Player - transform.position);
        float distance = toward.magnitude;
        bool seesPlayer = TrackPlayer(distance, 28, 32);
        if (seesPlayer || distance < 5f)
        {
            lastKnownPlayer = encounter.Player;
            alertUntil = Time.time + 12;
        }

        switch (state)
        {
            case State.Roar:
                Turn(toward, 3);
                if (Time.time >= deadline) BeginBossState(State.Hunt, 0);
                return;
            case State.Windup:
                // Aim during the first part of the tell, then commit to a straight
                // charge so the player can dodge it deliberately.
                if (Mathf.InverseLerp(bossStateStarted, deadline, Time.time) < .52f)
                    chargeDirection = toward.normalized;
                Turn(chargeDirection, 8);
                if (Time.time >= deadline)
                {
                    BeginBossState(State.Charge, IsEnraged ? 1.25f : 1.45f);
                    bossLastProp = null;
                    Call(1);
                }
                return;
            case State.Charge:
                BossCharge();
                return;
            case State.MeleeWindup:
                if (Mathf.InverseLerp(bossStateStarted, deadline, Time.time) < .55f)
                    chargeDirection = toward.normalized;
                Turn(chargeDirection, 8);
                if (Time.time >= deadline) BeginBossState(State.Melee, .32f);
                return;
            case State.Melee:
                // The strike has a short forward step, a limited arc, and one hit.
                if (distance > 1.5f)
                    Step(chargeDirection * Mathf.Min(.7f, distance - 1.5f), IsEnraged ? 3.5f : 2.6f);
                toward = Flat(encounter.Player - transform.position);
                if (!bossAttackHit && toward.magnitude < 2.45f &&
                    Vector3.Dot(chargeDirection, toward.normalized) > .25f &&
                    Mathf.Abs(encounter.Player.y - transform.position.y - 1) < 1.25f &&
                    VisibleFrom(encounter.View.transform.position))
                {
                    bossAttackHit = true;
                    encounter.Hurt(IsEnraged ? 34 : 28);
                    encounter.Effects.PropImpact(encounter.Player, false);
                }
                if (Time.time >= deadline)
                {
                    BeginBossState(State.Recover, IsEnraged ? .48f : .72f);
                    nextAttack = deadline + .16f;
                }
                return;
            case State.Stunned:
            case State.Recover:
                if (Time.time >= deadline)
                {
                    BeginBossState(State.Hunt, 0);
                    nextPath = 0;
                }
                return;
        }

        if ((!bossAwake && Time.time < alertUntil) || (IsEnraged && !bossEnrageShown))
        {
            bossAwake = true;
            bossEnrageShown |= IsEnraged;
            BeginBossState(State.Roar, IsEnraged ? .9f : 1.05f);
            Call(1);
            return;
        }

        if (Time.time < alertUntil)
        {
            if (seesPlayer && distance < 2.25f && Time.time >= nextAttack)
            {
                chargeDirection = toward.normalized;
                BeginBossState(State.MeleeWindup, IsEnraged ? .44f : .67f);
                Call(.78f);
                return;
            }
            if (seesPlayer && distance < 1.7f)
            {
                Turn(toward, 6);
                return;
            }
            bool clearChargeLane = !Obstacle(transform.position, toward.normalized, distance, out Collider barrier) ||
                barrier.GetComponentInParent<FacilityShootableProp>();
            if (seesPlayer && distance > 5 && distance < 17 && clearChargeLane && Time.time >= nextAttack)
            {
                chargeDirection = toward.normalized;
                BeginBossState(State.Windup, IsEnraged ? .68f : .95f);
                Call(.9f);
                return;
            }
            Vector3 target = lastKnownPlayer;
            if (!seesPlayer && Flat(target - transform.position).magnitude < 1)
            {
                // Search beyond the last seen position instead of stopping there.
                float angle = patrolIndex * Mathf.PI * .5f;
                target += new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 3;
                if (Flat(target - transform.position).magnitude < 1.1f || !Within(target))
                    patrolIndex = (patrolIndex + 1) % 4;
            }
            BossNavigate(target, IsEnraged ? 3.65f : 2.8f);
            return;
        }

        Vector3 waypoint = (patrolIndex % 3) switch
        {
            0 => new Vector3(0, encounter.Elevation, -8),
            1 => new Vector3(2, encounter.Elevation, 0),
            _ => new Vector3(-1, encounter.Elevation, 10)
        };
        BossNavigate(waypoint, 1.8f);
        if (Flat(transform.position - waypoint).magnitude < 1.1f) patrolIndex++;
    }

    private void BossCharge()
    {
        if (Time.time >= deadline)
        {
            BeginBossState(State.Recover, .8f);
            nextAttack = deadline + .5f;
            return;
        }
        float stride = (IsEnraged ? 13 : 11) * Time.deltaTime;
        if (!Within(transform.position + chargeDirection * stride))
        {
            Stun(IsEnraged ? 1.6f : 2.2f);
            return;
        }
        if (Obstacle(transform.position, chargeDirection, stride + .08f, out Collider shape, true))
        {
            if (shape.GetComponentInParent<PlayerMovement>())
            {
                encounter.Hurt(IsEnraged ? 43 : 37);
                BeginBossState(State.Recover, .85f);
                nextAttack = deadline + .35f;
            }
            else if (shape.GetComponentInParent<FacilityShootableProp>())
            {
                if (shape != bossLastProp || Time.time >= bossNextPropHit)
                {
                    bossLastProp = shape;
                    bossNextPropHit = Time.time + .4f;
                    encounter.HitCrate(shape, chargeDirection);
                }
            }
            else Stun(IsEnraged ? 1.6f : 2.2f);
            return;
        }
        transform.position += chargeDirection * stride;
    }

    private void BossNavigate(Vector3 target, float speed)
    {
        Vector3 before = transform.position;
        Vector3 direction = Flat(target - before).normalized;
        if (Time.time >= bossNextPropHit && Obstacle(before, direction, 1.05f, out Collider cargo) &&
            cargo.GetComponentInParent<FacilityShootableProp>())
        {
            encounter.HitCrate(cargo, direction);
            bossNextPropHit = Time.time + .7f;
            nextPath = 0;
        }
        Navigate(target, speed);
        if ((transform.position - before).sqrMagnitude > .00001f)
        {
            bossStuckTime = 0;
            return;
        }
        bossStuckTime += Time.deltaTime;
        if (bossStuckTime < .55f) return;
        // Try both sides of a blocked route using collision-checked steps.
        for (int i = 0; i < 4; i++)
        {
            float angle = (i % 2 == 0 ? bossAvoidSide : -bossAvoidSide) * (i < 2 ? 60 : 105);
            Vector3 escape = Quaternion.Euler(0, angle, 0) * direction;
            if (!Within(before + escape) || !Walkable(before + escape) ||
                Obstacle(before, escape, 1, out _)) continue;
            Step(escape, speed);
            nextPath = 0;
            break;
        }
        if (bossStuckTime > 1.8f)
        {
            bossAvoidSide *= -1;
            patrolIndex++;
            bossStuckTime = .6f;
            nextPath = 0;
        }
    }

    private void Stun(float duration)
    {
        BeginBossState(State.Stunned, duration);
        nextAttack = deadline + .6f;
        nextPath = 0;
        encounter.Effects.PropImpact(transform.position + transform.forward, true);
        Call(.8f);
    }

    private void Leech()
    {
        float distance = Flat(encounter.Player - transform.position).magnitude;
        bool seesPlayer = TrackPlayer(distance, 15, 14);
        if (state == State.Discharge)
        {
            Turn(Flat(dischargeTarget - transform.position), 16);
            if (Time.time >= deadline)
            {
                ReleaseDischarge();
                nextAttack = Time.time + 2.4f / encounter.Difficulty;
                retreatUntil = Time.time + .9f;
                state = State.Evade;
                nextEscape = 0;
            }
            return;
        }

        bool threatened = seesPlayer && Looking(.95f);
        if (seesPlayer && distance <= 8.5f && Time.time >= nextAttack &&
            (!threatened || distance < 4.6f || Time.time - lastHit < 1f))
        {
            state = State.Discharge;
            dischargeTarget = encounter.Player + Vector3.up * .28f;
            deadline = Time.time + .58f;
            Call(.85f);
            return;
        }

        if (threatened || Time.time < retreatUntil)
        {
            if (state != State.Evade || Time.time >= nextEscape)
            {
                wanderTarget = EscapeTarget();
                nextEscape = Time.time + .55f;
            }
            state = State.Evade;
            Navigate(wanderTarget, 5.3f);
            return;
        }

        if (encounter.Powered)
        {
            Vector3 site = encounter.Panel + Vector3.forward * .9f;
            if (Flat(site - transform.position).magnitude > .8f)
            {
                state = State.Hunt;
                Navigate(site, 3.6f);
            }
            else if (state != State.Drain)
            {
                state = State.Drain;
                deadline = Time.time + 1.25f;
                Call(.88f);
            }
            else if (Time.time >= deadline)
            {
                encounter.DrainPanel();
                state = State.Evade;
                wanderTarget = EscapeTarget();
            }
            return;
        }

        if (Time.time < alertUntil && Flat(lastKnownPlayer - transform.position).magnitude > 2.2f)
        {
            Navigate(lastKnownPlayer, 3.25f);
            return;
        }
        if (wanderTarget == Vector3.zero || Flat(wanderTarget - transform.position).magnitude < 1.2f)
            wanderTarget = EscapeTarget();
        Navigate(wanderTarget, 2.45f);
    }

    private void ReleaseDischarge()
    {
        Vector3 origin = AimPoint + transform.forward * (radius + .18f);
        Vector3 delta = dischargeTarget - origin;
        if (delta.sqrMagnitude < .0025f) return;
        // The discharge follows the point marked during the windup, so strafing or cover can defeat it.
        Vector3 end = origin + delta.normalized * Mathf.Min(9, delta.magnitude + .5f);
        if (Physics.Raycast(origin, delta.normalized, out RaycastHit hit, Vector3.Distance(origin, end), ~0, QueryTriggerInteraction.Ignore))
        {
            end = hit.point;
            if (hit.collider.GetComponentInParent<PlayerMovement>())
                encounter.Hurt(18);
        }
        encounter.Effects.Discharge(origin, end);
    }

    private bool TrackPlayer(float distance, float sightRange, float hearingRange)
    {
        bool seesPlayer = distance < sightRange && VisibleFrom(encounter.View.transform.position);
        if (seesPlayer)
        {
            lastKnownPlayer = encounter.Player;
            alertUntil = Time.time + 3.2f;
        }
        else if (encounter.HeardShot(transform.position, hearingRange, out Vector3 sound))
        {
            lastKnownPlayer = sound;
            alertUntil = Time.time + 2.4f;
        }
        return seesPlayer;
    }

    private Vector3 EscapeTarget()
    {
        Vector3 eye = encounter.View.transform.position;
        Vector3 best = spawn;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI / 6;
            Vector3 candidate = transform.position + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (i % 2 == 0 ? 6 : 9);
            candidate.y = encounter.Elevation;
            if (!Within(candidate) || !Walkable(candidate)) continue;
            Vector3 offset = candidate + Vector3.up * .55f - eye;
            bool covered = Physics.Raycast(eye, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore);
            float score = offset.magnitude + (covered ? 13 : 0) - Mathf.Max(0, Vector3.Dot(offset.normalized, encounter.View.transform.forward)) * 8;
            if (score > bestScore) { best = candidate; bestScore = score; }
        }
        return best;
    }

    private void StartDrop()
    {
        state = State.Drop;
        Call(.85f);
    }

    private void Stalker()
    {
        Vector3 playerOffset = Flat(encounter.Player - transform.position);
        float distance = playerOffset.magnitude;
        if (state == State.Perch)
        {
            bool seesPlayer = TrackPlayer(distance, 23, 25);
            bool ambush = seesPlayer && !Looking(.87f) && distance < (encounter.Distracted ? 15 : 8);
            Vector3 landing = transform.position;
            landing.y = encounter.Elevation;
            if ((encounter.Powered || ambush) && Walkable(landing))
                StartDrop();
            else if (Time.time < alertUntil && !Looking(.77f))
                Step(lastKnownPlayer - transform.position, 1.45f);
            return;
        }
        if (state == State.Drop)
        {
            Vector3 floor = transform.position;
            floor.y = encounter.Elevation;
            transform.position = Vector3.MoveTowards(transform.position, floor, Time.deltaTime * 10);
            if (transform.position.y <= floor.y + .01f) state = State.Hunt;
            return;
        }
        if (encounter.Powered)
        {
            state = State.Flee;
            Vector3 refuge = new(6.5f, encounter.Elevation, 10.5f);
            Navigate(refuge, 4.7f);
            return;
        }
        if (state == State.PounceWindup)
        {
            Turn(chargeDirection, 17);
            if (Time.time >= deadline)
            {
                pounceOrigin = transform.position;
                state = State.Pounce;
                deadline = Time.time + .29f;
            }
            return;
        }
        if (state == State.Pounce)
        {
            Vector3 before = transform.position;
            Step(chargeDirection * 2.6f, 9.3f);
            playerOffset = Flat(encounter.Player - transform.position);
            bool reachedPlayer = (transform.position - pounceOrigin).sqrMagnitude > .015f &&
                playerOffset.magnitude < 1.18f && Vector3.Dot(chargeDirection, playerOffset.normalized) > -.15f &&
                Mathf.Abs(encounter.Player.y - transform.position.y - .95f) < .65f &&
                VisibleFrom(encounter.View.transform.position);
            if (reachedPlayer) encounter.Hurt(27);
            if (reachedPlayer || Time.time >= deadline || (before - transform.position).sqrMagnitude < .000001f)
            {
                state = State.Recover;
                deadline = Time.time + .57f;
            }
            return;
        }
        if (state == State.Recover && Time.time < deadline) return;

        state = State.Hunt;
        bool playerVisible = TrackPlayer(distance, 23, 25);
        if (playerVisible && distance < 2.8f && distance > .05f)
        {
            chargeDirection = playerOffset.normalized;
            state = State.PounceWindup;
            deadline = Time.time + .46f / encounter.Difficulty;
            Call(.8f);
        }
        else if (Time.time < alertUntil && (!Looking(.84f) || distance < 4.5f))
        {
            Navigate(lastKnownPlayer, 4.6f);
        }
    }

    private void Swarmer()
    {
        int cluster = 1;
        Vector3 separation = Vector3.zero;
        Vector3 cohesion = Vector3.zero;
        foreach (FacilityCreature other in encounter.Enemies)
        {
            if (other == this || !other || !other.Alive) continue;
            Vector3 offset = Flat(other.transform.position - transform.position);
            float distance = offset.magnitude;
            if (distance > 4.3f) continue;
            cluster++;
            cohesion += offset;
            if (distance < .68f) separation -= offset.normalized * (.68f - distance);
        }
        Vector3 player = Flat(encounter.Player - transform.position);
        bool seesPlayer = TrackPlayer(player.magnitude, 17, 21);
        if (player.magnitude < .9f && Time.time >= nextAttack && seesPlayer)
        {
            encounter.Hurt(8);
            nextAttack = Time.time + 1.13f / encounter.Difficulty;
            Call(.48f);
        }
        Vector3 target = (Time.time < alertUntil ? lastKnownPlayer : spawn) + separation * 2.1f + cohesion * .11f;
        float flank = cluster >= 3 && player.magnitude > 2.2f ? Mathf.Clamp(spawn.x, -1.7f, 1.7f) : 0;
        target += transform.right * (flank + Mathf.Sin(walkPhase + spawn.x * 5) * (cluster >= 3 ? .13f : .72f));
        Navigate(target, cluster >= 3 ? 4.55f : 2.275f);
    }

    private void Call(float volume)
    {
        audioSource.pitch = species == Species.Goliath ? Random.Range(.66f, .84f) : Random.Range(.87f, 1.14f);
        audioSource.PlayOneShot(encounter.Effects.Voice(species), volume);
    }

    private static Vector3 Flat(Vector3 vector) { vector.y = 0; return vector; }

    private void Turn(Vector3 heading, float speed)
    {
        if (heading.sqrMagnitude > .001f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(heading), Time.deltaTime * speed);
    }

    private bool Within(Vector3 point)
    {
        if (species == Species.Leech) return Mathf.Abs(point.x) < 18.9f && point.z > 15.8f && point.z < 44.3f;
        float limit = species == Species.Goliath ? 14.2f : 12.3f;
        return Mathf.Abs(point.x) < (species == Species.Goliath ? 19 : 14.2f) && Mathf.Abs(point.z) < limit;
    }

    private bool Obstacle(Vector3 start, Vector3 heading, float distance, out Collider closest, bool hitPlayer = false)
    {
        float margin = radius * .88f;
        Vector3 bottom = start + Vector3.up * (margin + .07f);
        Vector3 top = start + Vector3.up * Mathf.Max(margin + .07f, height - margin);
        int count = Physics.CapsuleCastNonAlloc(bottom, top, margin, heading, sweep, distance, ~0, QueryTriggerInteraction.Ignore);
        closest = null;
        float first = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider shape = sweep[i].collider;
            if (shape.GetComponentInParent<FacilityCreature>() || (!hitPlayer && shape.transform == encounter.View.transform.root)) continue;
            if (sweep[i].distance >= first) continue;
            closest = shape;
            first = sweep[i].distance;
        }
        return closest;
    }

    private bool Walkable(Vector3 point)
    {
        float margin = radius * .83f;
        Vector3 bottom = point + Vector3.up * (margin + .08f);
        Vector3 top = point + Vector3.up * Mathf.Max(margin + .08f, height - margin);
        foreach (Collider shape in Physics.OverlapCapsule(bottom, top, margin, ~0, QueryTriggerInteraction.Ignore))
            if (!shape.GetComponentInParent<FacilityCreature>() && shape.transform != encounter.View.transform.root)
                return false;
        return true;
    }

    private void Step(Vector3 offset, float speed)
    {
        Vector3 heading = Flat(offset).normalized;
        if (heading.sqrMagnitude < .01f) return;
        Turn(heading, 11);
        float distance = Mathf.Min(Flat(offset).magnitude, speed * Time.deltaTime);
        if (Within(transform.position + heading * distance) && !Obstacle(transform.position, heading, distance + .035f, out _))
            transform.position += heading * distance;
    }

    private void Navigate(Vector3 target, float speed)
    {
        Vector3 offset = Flat(target - transform.position);
        if (offset.sqrMagnitude < .06f) return;
        if (!Obstacle(transform.position, offset.normalized, offset.magnitude, out _))
        {
            path.Clear();
            Step(offset, speed);
            return;
        }
        if (Time.time >= nextPath)
        {
            nextPath = Time.time + Random.Range(.45f, .72f);
            FindPath(target);
        }
        if (path.Count == 0) return;
        if (Flat(path[0] - transform.position).magnitude < .42f)
            path.RemoveAt(0);
        if (path.Count > 0) Step(path[0] - transform.position, speed);
    }

    private static readonly Vector2Int[] GridDirections = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

    private void FindPath(Vector3 target)
    {
        path.Clear();
        float spacing = species == Species.Goliath ? .85f : 1.25f;
        Vector2Int Cell(Vector3 point) => new(Mathf.RoundToInt(point.x / spacing), Mathf.RoundToInt(point.z / spacing));
        Vector3 World(Vector2Int cell) => new(cell.x * spacing, encounter.Elevation, cell.y * spacing);
        Vector2Int start = Cell(transform.position), goal = Cell(target), best = start;
        var open = new List<Vector2Int> { start };
        var prior = new Dictionary<Vector2Int, Vector2Int>();
        var costs = new Dictionary<Vector2Int, int> { [start] = 0 };
        var blocked = new HashSet<Vector2Int>();

        int searchBudget = species == Species.Goliath ? 720 : 240;
        for (int iteration = 0; iteration < searchBudget && open.Count > 0; iteration++)
        {
            int index = 0;
            float score = float.PositiveInfinity;
            for (int i = 0; i < open.Count; i++)
            {
                float estimate = costs[open[i]] + Vector2Int.Distance(open[i], goal) * 1.3f;
                if (estimate < score) { index = i; score = estimate; }
            }
            Vector2Int cell = open[index];
            open.RemoveAt(index);
            if ((cell - goal).sqrMagnitude < (best - goal).sqrMagnitude) best = cell;
            if (cell == goal) break;
            foreach (Vector2Int direction in GridDirections)
            {
                Vector2Int step = cell + direction;
                int cost = costs[cell] + 1;
                if (blocked.Contains(step) || (costs.TryGetValue(step, out int old) && old <= cost)) continue;
                Vector3 next = World(step);
                Vector3 source = cell == start ? transform.position : World(cell);
                Vector3 delta = Flat(next - source);
                if (!Within(next) || !Walkable(next))
                {
                    blocked.Add(step);
                    continue;
                }
                if (Obstacle(source, delta.normalized, delta.magnitude, out _)) continue;
                prior[step] = cell;
                costs[step] = cost;
                if (!open.Contains(step)) open.Add(step);
            }
        }

        for (Vector2Int cell = best; cell != start; cell = prior[cell])
            path.Add(World(cell));
        path.Reverse();
    }
}

