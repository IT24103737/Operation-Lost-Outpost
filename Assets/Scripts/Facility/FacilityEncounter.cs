using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public sealed partial class FacilityEncounter : MonoBehaviour
{
    [SerializeField] private GameObject weapon;
    [SerializeField] private GameObject pallet;
    [SerializeField] private GameObject barrel;
    [SerializeField] private Material concrete;
    [SerializeField] private Material steel;
    [SerializeField] private Material hazard;
    [SerializeField] private Material wood;

    internal enum Species { Goliath, Leech, Stalker, Swarmer }
    internal enum StepSurface { Concrete, Steel, Wood, Plastic, Cardboard }

    private static readonly string[] FloorNames = { "Outbound Dispatch", "Grid & Quarantine", "Blackout Research", "Waste Processing" };
    private static readonly string[] EnemyNames = { "Biomass Goliath", "Volt-Leech", "Shadow Stalker", "Swarmers" };
    private readonly List<FacilityCreature> enemies = new();
    private readonly List<FacilityShootableLight> poweredLights = new();
    private readonly List<Transform> chargeCrates = new();
    private Transform temporaryFloor, liftDoor, panel, flash;
    private FacilityWeaponView weaponView;
    private Material texturedConcrete;
    private Light flashlight;
    private AudioSource playerAudio, roomAudio, roomTone, footstepAudio;
    private FacilityEffects effects;
    private Camera view;
    private float readyFov;
    private PlayerMovement movement;
    private MouseLook mouseLook;
    private Rigidbody playerBody;
    private int floor, health = 100, ammo = 30, reserve = 150;
    private float nextShot, reloadEnd, muzzleEnd, hitEnd, hurtEnd, distractionEnd, panelUse;
    private bool powered, finished;
    private string message;
    private float messageEnd;
    private float noiseEnd;
    private Vector3 noisePosition;
    private readonly RaycastHit[] clearanceHits = new RaycastHit[16];
    private readonly Collider[] clearanceOverlaps = new Collider[12];
    private FacilityShootableProp aimedProp;
    private Vector3 aimedPoint;
    private float wallClearance, objectHitEnd, footstepAt;

    internal Camera View => view;
    internal Vector3 Player => playerBody.position;
    internal float Elevation => floor == 0 ? .11f : floor == 1 ? 11f : 23f + (floor - 2) * 12f;
    internal bool Powered => powered;
    internal bool Distracted => Time.time < distractionEnd;
    internal Vector3 Panel => panel.position;
    internal IReadOnlyList<FacilityCreature> Enemies => enemies;
    internal bool PlayerAlive => health > 0 && !finished;
    internal float Difficulty => 1 + floor * .22f;
    internal FacilityEffects Effects => effects;
    internal bool HeardShot(Vector3 listener, float distance, out Vector3 source)
    {
        source = noisePosition;
        return Time.time < noiseEnd && (listener - source).sqrMagnitude < distance * distance;
    }

    private void Start()
    {
        view = Camera.main;
        movement = FindFirstObjectByType<PlayerMovement>();
        if (!view || !movement || !weapon)
        {
            Debug.LogError("Facility combat requires the player, main camera, and weapon prefab.", this);
            enabled = false;
            return;
        }

        mouseLook = view.GetComponent<MouseLook>();
        playerBody = movement.GetComponent<Rigidbody>();
        view.transform.localPosition = Vector3.up * .64f;
        view.nearClipPlane = .06f;
        readyFov = view.fieldOfView;
        UniversalAdditionalCameraData cameraData = view.GetUniversalAdditionalCameraData();
        cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cameraData.antialiasingQuality = AntialiasingQuality.High;
        if (movement.TryGetComponent(out Renderer playerMesh))
            playerMesh.enabled = false;
        effects = new FacilityEffects();
        texturedConcrete = effects.WeatheredConcrete(concrete);
        BuildEnvironmentFinishes();
        playerAudio = view.gameObject.AddComponent<AudioSource>();
        footstepAudio = view.gameObject.AddComponent<AudioSource>();
        roomAudio = view.gameObject.AddComponent<AudioSource>();
        roomTone = view.gameObject.AddComponent<AudioSource>();
        roomTone.loop = true;
        roomTone.volume = .23f;
        flashlight = new GameObject("Player flashlight").AddComponent<Light>();
        flashlight.transform.SetParent(view.transform, false);
        flashlight.type = LightType.Spot;
        flashlight.range = 19;
        flashlight.spotAngle = 55;
        flashlight.intensity = 12;
        flashlight.color = new Color(.79f, .87f, 1);
        flashlight.enabled = false;
        Transform gun = view.transform.Find(weapon.name);
        if (!gun)
            gun = Instantiate(weapon, view.transform).transform;
        weaponView = new FacilityWeaponView(view.transform, gun, effects.Flesh, steel);
        flash = weaponView.Muzzle;
        Light muzzle = flash.gameObject.AddComponent<Light>();
        muzzle.type = LightType.Point;
        muzzle.color = new Color(1, .7f, .3f);
        muzzle.range = 5;
        muzzle.intensity = 9;
        flash.gameObject.SetActive(false);
        Raise("Warehouse_Expansion", 11f);
        Raise("Returns - Salvage and Quarantine", 11f);
        EnterFloor();
    }

    private static void Raise(string rootName, float height)
    {
        GameObject root = GameObject.Find(rootName);
        if (root)
            root.transform.position += Vector3.up * height;
    }

    private void EnterFloor()
    {
        if (temporaryFloor)
        {
            temporaryFloor.gameObject.SetActive(false);
            Destroy(temporaryFloor.gameObject);
        }
        enemies.Clear();
        footstepAt = 0;
        health = 100;
        displayedHealth = 1;
        hurtEnd = 0;
        noiseEnd = 0;
        poweredLights.Clear();
        chargeCrates.Clear();
        temporaryFloor = new GameObject(FloorNames[floor] + " encounter").transform;
        float y = Elevation;
        float centerZ = floor == 1 ? 30 : 0;
        SetZoneActive("Warehouse_Expansion", floor == 1);
        SetZoneActive("Returns - Salvage and Quarantine", floor == 1);
        SetZoneActive("Generated_Warehouse13", floor == 0);
        SetZoneActive("Dispatch - Interrupted Shift", floor == 0);
        SetZoneActive("WarehouseNightUpgrade", floor == 0);
        ImproveExistingSigns(floor == 0 ? "Dispatch - Interrupted Shift" : "Returns - Salvage and Quarantine");
        roomTone.clip = effects.RoomTone(floor);
        roomTone.Play();
        if (floor < 2)
        {
            Box("Continuous warehouse roof", new Vector3(0, y + 5.99f, centerZ),
                new Vector3(40.5f, .32f, 30.5f), texturedConcrete);
            if (floor == 1)
                for (int x = -16; x <= 16; x += 8)
                    Box("Quarantine roof joist", new Vector3(x, y + 5.72f, centerZ),
                        new Vector3(.28f, .3f, 30), steel);
            if (floor == 0)
            {
                SetGroupLights("Dispatch - Interrupted Shift", true, false);
                SetGroupLights("Generated_Warehouse13", true, false);
                SetGroupLights("WarehouseNightUpgrade", true, false);
                Box("Loading entrance shutter", new Vector3(0, y + 2.5f, -14.9f), new Vector3(12, 5, .35f), steel);
                Box("Freight bulkhead", new Vector3(0, y + 2, 15), new Vector3(4, 4, .5f), steel);
                foreach (float partition in new[] { -4.8f, 5.8f })
                foreach (int side in new[] { -1, 1 })
                {
                    Box("Dispatch bay divider", new Vector3(side * 5.5f, y + 1.6f, partition),
                        new Vector3(5.2f, 3.2f, .26f), steel);
                    Box("Divider safety stripe", new Vector3(side * 5.5f, y + 1.15f, partition - .14f),
                        new Vector3(5.2f, .125f, .02f), hazard, false);
                }
            }
            else
            {
                SetGroupLights("Returns - Salvage and Quarantine", false, true);
                Box("Intake bulkhead", new Vector3(0, y + 3, 15), new Vector3(40, 6, .5f), steel);
                Box("Quarantine steel threshold", new Vector3(0, y + .026f, 20.7f), new Vector3(4.5f, .058f, 2.4f), steel);
                PlaceLight(new Vector3(-8, y + 4, 24), new Color(.65f, .9f, 1), 28, 16, true);
                PlaceLight(new Vector3(9, y + 4, 37), new Color(.65f, .9f, 1), 30, 18, true);
                PlaceLight(new Vector3(0, y + 4.6f, 19), new Color(.85f, .2f, .13f), 13, 14, false);
            }
        }
        else
            BuildServiceFloor(y);
        if (floor >= 2)
            MountSign(floor == 2 ? "RESEARCH LAB" : "WASTE PROCESSING", "FREIGHT LIFT", new Vector3(0, y + 3.66f, 12.74f));
        if (floor < 2)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (root.activeSelf && root.name is "Dispatch - Interrupted Shift" or "Returns - Salvage and Quarantine" or
                    "Warehouse_Expansion" or "Generated_Warehouse13" or "WarehouseNightUpgrade")
                    foreach (Transform prop in root.GetComponentsInChildren<Transform>())
                    {
                        FacilityShootableProp.Register(prop);
                        ApplyWarehouseFinish(prop);
                    }
            }
        }
        aimedProp = null;
        wallClearance = 0;

        Vector3 spawn = new Vector3(floor == 0 ? 0 : -1, y + 1.03f, centerZ - 11);
        playerBody.position = spawn;
        playerBody.rotation = Quaternion.identity;
        playerBody.linearVelocity = Vector3.zero;
        if (mouseLook)
            mouseLook.ResetLook();
        playerBody.transform.SetPositionAndRotation(spawn, Quaternion.identity);

        Vector3 control = new Vector3(-3.5f, y + .8f, centerZ - 8);
        panel = Box("Power restoration panel", control, new Vector3(.63f, 1.6f, .42f), steel);
        Box("Panel indicator", control + new Vector3(0, .43f, -.23f), new Vector3(.42f, .25f, .075f), hazard).SetParent(panel);
        Vector3 gate = new Vector3(0, y + 1.5f, centerZ + (floor < 2 ? 13.8f : 12.1f));
        liftDoor = Box("Freight lift shutter", gate + Vector3.forward * .45f, new Vector3(3.4f, 3, .26f), steel);
        Box("Freight call station", gate + new Vector3(2.05f, -.8f, 0), new Vector3(.42f, 1.4f, .4f), hazard);
        SetPower(floor == 0 || floor == 3);
        switch ((Species)floor)
        {
            case Species.Goliath:
                Spawn(Species.Goliath, new Vector3(-3, y, 3));
                foreach (Vector3 offset in new[] { new Vector3(-2.3f, .46f, -1), new Vector3(2.4f, .46f, 2), new Vector3(-1.9f, .46f, 7) })
                {
                    Transform crate = Box("Breakable transit crate", offset + Vector3.up * y, new Vector3(.96f, .92f, .96f), wood);
                    for (int sign = -1; sign <= 1; sign += 2)
                        Box("Crate strap", crate.position + Vector3.right * sign * .28f + Vector3.up * .468f,
                            new Vector3(.085f, .018f, .965f), steel, false).SetParent(crate);
                    FacilityShootableProp.Register(crate);
                    chargeCrates.Add(crate);
                }
                break;
            case Species.Leech:
                Spawn(Species.Leech, new Vector3(7, y, 30));
                Spawn(Species.Leech, new Vector3(-4, y, 32));
                Spawn(Species.Leech, new Vector3(9, y, 39));
                Spawn(Species.Leech, new Vector3(-8, y, 36));
                break;
            case Species.Stalker:
                Spawn(Species.Stalker, new Vector3(3, y + 3.62f, 1));
                Spawn(Species.Stalker, new Vector3(-4, y + 3.62f, 5));
                Spawn(Species.Stalker, new Vector3(4.8f, y + 3.62f, -5.8f));
                break;
            case Species.Swarmer:
                for (int i = 0; i < 6; i++)
                    Spawn(Species.Swarmer, new Vector3(-2.4f + i * .96f, y, 4.5f + i % 2));
                break;
        }
        Physics.SyncTransforms();
        foreach (FacilityCreature enemy in enemies)
            enemy.InitializePath();
        Say(floor == 2 ? "Restore the lights. Watch the ceiling." : "Find the freight lift at the far end.");
    }

    private void BuildServiceFloor(float y)
    {
        bool lab = floor == 2;
        Box("Sealed concrete slab", new Vector3(0, y - .25f, 0), new Vector3(30, .5f, 26), texturedConcrete);
        Box("Ceiling", new Vector3(0, y + 4.75f, 0), new Vector3(30, .35f, 26), texturedConcrete);
        foreach (int sign in new[] { -1, 1 })
        {
            Box("Service wall", new Vector3(sign * 15, y + 2.3f, 0), new Vector3(.45f, 4.6f, 26), texturedConcrete);
            Box("Sealed bulkhead", new Vector3(0, y + 2.3f, sign * 13), new Vector3(30, 4.6f, .45f), texturedConcrete);
            Box("Overhead conduit", new Vector3(sign * 10, y + 4.35f, 0), new Vector3(.29f, .22f, 25), steel);
            for (int i = 0; i < 4; i++)
            {
                float z = -8.5f + i * 5.5f;
                float x = sign * (i % 2 == 0 ? 8 : 10.5f);
                Box(lab ? "Observation booth wall" : "Waste sorting divider", new Vector3(x, y + 1.45f, z),
                    new Vector3(lab ? 4.1f : 3.3f, 2.9f, lab ? .4f : 1.1f), steel);
                Box("Wall safety stripe", new Vector3(x, y + 1.15f, z - (lab ? .207f : .56f)),
                    new Vector3(3, .13f, .018f), hazard, false);
                PutProp(lab ? pallet : barrel, new Vector3(x + sign * 2.8f, y, z + 1.1f), i * 43);
                PlaceLight(new Vector3(sign * 6.5f, y + 4.2f, z),
                    lab ? new Color(.66f, .84f, 1) : new Color(.68f, .88f, .35f), lab ? 25 : 22, 12, true);
                Box("Overhead fixture housing", new Vector3(sign * 6.5f, y + 4.45f, z), new Vector3(.42f, .14f, 1.7f), steel);
                for (int j = 0; j < 4; j++)
                    Box("Discarded scrap", new Vector3(x + Mathf.Sin(j * 5) * 2, y + .024f, z + .8f + j * .38f),
                        new Vector3(.13f + j * .05f, .035f, .52f), j % 2 == 0 ? steel : hazard, false)
                        .rotation = Quaternion.Euler(0, j * 71 + i * 31, 0);
            }
        }
        if (lab)
        {
            Box("Dark isolation corridor", new Vector3(0, y + 1.7f, 7.4f), new Vector3(8, 3.4f, .36f), steel);
            for (int i = 0; i < 4; i++)
            {
                Vector3 basePosition = new(-12.25f, y + .19f, -4.9f + 2.1f * i);
                Box("Specimen pod plinth", basePosition, new Vector3(1.65f, .38f, 1.65f), steel);
                Cylinder("Sealed specimen pod", basePosition + Vector3.up * 1.41f,
                    new Vector3(1.17f, 1.21f, 1.17f), texturedConcrete, true);
                Cylinder("Specimen pod upper collar", basePosition + Vector3.up * 2.56f,
                    new Vector3(1.3f, .08f, 1.3f), steel, false);
                Box("Specimen pod status panel", basePosition + new Vector3(.6f, 1.28f, 0),
                    new Vector3(.08f, .45f, .43f), effects.Indicator(i == 1), false);
            }
            PlaceLight(new Vector3(0, y + 4, -10), new Color(.65f, .15f, .13f), 14, 13, false);
            PlaceLight(new Vector3(0, y + 4, 10), new Color(.61f, .14f, .18f), 12, 9, false);
        }
        else
        {
            Box("Waste trench cover", new Vector3(0, y + .023f, -.5f), new Vector3(2.1f, .054f, 15), steel);
            for (int i = 0; i < 5; i++)
            {
                float x = (i % 2 == 0 ? 1 : -1) * 1.65f;
                float z = -6 + i * 3;
                Box("Steel cradle deck", new Vector3(x, y + .54f, z), new Vector3(2.22f, .09f, .94f), steel);
                foreach (int end in new[] { -1, 1 })
                    Box("Steel cradle leg", new Vector3(x + end * 1.03f, y + .25f, z),
                        new Vector3(.16f, .50f, .84f), steel);
                FacilityShootableProp.Register(Box("Raised crate barricade",
                    new Vector3(x, y + .975f, z), new Vector3(2.2f, .78f, .92f), wood));
            }
            for (int i = 0; i < 11; i++)
            {
                float x = 10 + Mathf.Sin(i * 2.15f) * 2.7f;
                float z = 6 + Mathf.Cos(i * 1.7f) * 2;
                Cylinder("Runoff stain", new Vector3(x, y + .007f + i * .0004f, z),
                    new Vector3(2.45f - i * .15f, .003f, 1.7f - i * .09f), effects.Dark, false);
            }
            PutProp(barrel, new Vector3(9, y, 7.8f), 18);
            PutProp(barrel, new Vector3(10.2f, y, 8.35f), 68);
            PutProp(pallet, new Vector3(11.6f, y, 7.9f), 31);
        }
    }

    private void PutProp(GameObject prefab, Vector3 position, float heading)
    {
        if (!prefab) return;
        GameObject prop = Instantiate(prefab, position, Quaternion.Euler(0, heading, 0), temporaryFloor);
        var lodGroup = prop.GetComponentInChildren<LODGroup>();
        LOD[] lods = lodGroup ? lodGroup.GetLODs() : null;
        Renderer[] geometry = lods != null && lods.Length > 0 ? lods[0].renderers : prop.GetComponentsInChildren<Renderer>();
        float lowest = float.PositiveInfinity;
        foreach (Renderer renderer in geometry)
            if (renderer) lowest = Mathf.Min(lowest, renderer.bounds.min.y);
        if (!float.IsPositiveInfinity(lowest))
            prop.transform.position += Vector3.up * (position.y - lowest);
        FacilityShootableProp.Register(prop.transform);
    }

    private void SetGroupLights(string group, bool enabled, bool grid)
    {
        GameObject root = GameObject.Find(group);
        if (!root) return;
        foreach (Light light in root.GetComponentsInChildren<Light>())
        {
            if (light.type == LightType.Directional) continue;
            var target = RegisterLight(light);
            if (grid) poweredLights.Add(target);
            target.SetPowered(enabled);
        }
    }

    private void SetZoneActive(string name, bool active)
    {
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            if (root.name == name)
                root.SetActive(active);
    }

    private void PlaceLight(Vector3 position, Color color, float intensity, float range, bool grid)
    {
        Light light = new GameObject("Service light").AddComponent<Light>();
        light.transform.SetParent(temporaryFloor);
        light.transform.position = position;
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        FacilityShootableLight target = RegisterLight(light);
        if (grid) poweredLights.Add(target);
    }

    private FacilityShootableLight RegisterLight(Light light)
    {
        var existing = light.GetComponentInChildren<FacilityShootableLight>();
        if (existing) return existing;
        bool highBay = light.name == "Warehouse Light";
        Vector3 center = light.transform.position + Vector3.up * (highBay ? .24f : .1f);
        Transform lens = Box("Breakable light lens", center, new Vector3(highBay ? 1.45f : .62f, .095f, .34f), effects.Lamp);
        lens.SetParent(light.transform, true);
        var target = lens.gameObject.AddComponent<FacilityShootableLight>();
        target.Setup(light, effects.Lamp, steel);
        return target;
    }

    private void OnDestroy()
    {
        weaponView?.Dispose();
        effects?.Dispose();
        if (brushedSteel) Destroy(brushedSteel);
        if (corrugatedSteel) Destroy(corrugatedSteel);
        if (crateBoards) Destroy(crateBoards);
    }

    private Transform Box(string label, Vector3 position, Vector3 size, Material material, bool solid = true)
        => Primitive(PrimitiveType.Cube, label, position, size, material, solid);

    private void Cylinder(string label, Vector3 position, Vector3 size, Material material, bool solid)
        => Primitive(PrimitiveType.Cylinder, label, position, size, material, solid);

    private Transform Primitive(PrimitiveType type, string label, Vector3 position, Vector3 size, Material material, bool solid)
    {
        GameObject item = GameObject.CreatePrimitive(type);
        item.name = label;
        item.transform.SetParent(temporaryFloor);
        item.transform.position = position;
        item.transform.localScale = size;
        item.GetComponent<Renderer>().sharedMaterial = EnvironmentFinish(label, material);
        item.GetComponent<Collider>().enabled = solid;
        return item.transform;
    }

    private void Spawn(Species species, Vector3 position)
    {
        GameObject item = new(EnemyNames[(int)species]);
        item.transform.SetParent(temporaryFloor);
        item.transform.position = position;
        FacilityCreature creature = item.AddComponent<FacilityCreature>();
        creature.Setup(this, species);
        enemies.Add(creature);
    }

    private void SetPower(bool state)
    {
        powered = state;
        panel.GetChild(0).GetComponent<Renderer>().sharedMaterial = effects.Indicator(state);
        foreach (FacilityShootableLight light in poweredLights)
            light.SetPowered(state);
    }

    internal void DrainPanel()
    {
        if (!powered || floor != 1) return;
        SetPower(false);
        Say("GRID DRAINED  •  Freight lift locked");
    }

    internal bool HitCrate(Collider shape, Vector3 direction)
    {
        if (!chargeCrates.Remove(shape.transform)) return false;
        FacilityShootableProp prop = shape.GetComponent<FacilityShootableProp>();
        if (!prop) return false;
        if (Random.value < .40f)
            prop.Break(effects, shape.bounds.center, direction);
        else
        {
            prop.Push(shape.bounds.center, (direction + Vector3.up * .14f) * 10);
            var impact = shape.gameObject.AddComponent<FacilityCrateImpact>();
            impact.Setup(this);
        }
        return true;
    }

    internal void Hurt(int damage)
    {
        if (!PlayerAlive || Time.time < hurtEnd - .1f) return;
        health = Mathf.Max(0, health - Mathf.RoundToInt(damage * Difficulty));
        hurtEnd = Time.time + .26f;
        if (health == 0) EndRun();
    }

    private void EndRun()
    {
        movement.enabled = false;
        if (mouseLook) mouseLook.enabled = false;
        playerBody.linearVelocity = Vector3.zero;
        Cursor.lockState = CursorLockMode.None;
    }

    private bool Cleared()
    {
        foreach (FacilityCreature creature in enemies)
            if (creature && creature.Alive) return false;
        return true;
    }

    private bool Near(Vector3 point) => Vector3.Distance(Player, point) < 2.6f;

    private void Update()
    {
        if (!PlayerAlive)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                SceneManager.LoadScene(gameObject.scene.name);
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (Input.GetMouseButtonDown(0)) Cursor.lockState = CursorLockMode.Locked;
            return;
        }

        UpdateWeaponClearance();
        PlayFootsteps();
        if (reloadEnd > 0 && Time.time >= reloadEnd)
        {
            int amount = Mathf.Min(30 - ammo, reserve);
            ammo += amount;
            reserve -= amount;
            reloadEnd = 0;
        }
        if (Input.GetKeyDown(KeyCode.R)) Reload();
        if (Input.GetKeyDown(KeyCode.F)) flashlight.enabled = !flashlight.enabled;
        if (Input.GetKeyDown(KeyCode.V)) Say("Weapon finish  •  " + weaponView.CycleSkin());
        if (Input.GetMouseButton(0) && Time.time >= nextShot && reloadEnd == 0 && !movement.IsSprinting && wallClearance < .72f)
        {
            if (ammo > 0) Shoot();
            else if (Input.GetMouseButtonDown(0)) Reload();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (Near(Panel) && !powered)
            {
                SetPower(true);
                panelUse = Time.time + .8f;
                distractionEnd = Time.time + 2.4f;
                Say("Grid restored");
            }
            else if (NearLift())
            {
                if (!powered || !Cleared()) Say(!powered ? "Restore grid power to unlock the lift" : "Clear the floor to unlock the lift");
                else if (floor == 3)
                {
                    finished = true;
                    EndRun();
                }
                else
                {
                    floor++;
                    reserve += 45;
                    EnterFloor();
                }
            }
            else if (aimedProp)
                PushAimedProp();
        }
        liftDoor.localPosition = Vector3.MoveTowards(liftDoor.localPosition,
            new Vector3(0, Elevation + (powered && Cleared() ? 4.55f : 1.5f), (floor == 1 ? 30 : 0) + (floor < 2 ? 14.25f : 12.55f)),
            Time.deltaTime * 4);
        float reloadProgress = reloadEnd > 0 ? 1 - (reloadEnd - Time.time) / FacilityWeaponView.ReloadDuration : -1;
        bool aim = Input.GetMouseButton(1) && !movement.IsSprinting && reloadEnd == 0 && wallClearance < .4f;
        weaponView.Pose(movement.IsMoving, movement.IsSprinting, reloadProgress, Time.deltaTime, wallClearance, aim);
        view.fieldOfView = Mathf.Lerp(readyFov, readyFov * .72f, weaponView.AimBlend);
        if (mouseLook) mouseLook.AimSensitivity = Mathf.Lerp(1, .63f, weaponView.AimBlend);
        flash.gameObject.SetActive(Time.time < muzzleEnd);
    }

    private void PushAimedProp()
    {
        Vector3 forward = view.transform.forward;
        forward.y = 0;
        if (forward.sqrMagnitude < .01f) forward = movement.Heading;
        aimedProp.Shove(forward.normalized);
        effects.PropImpact(aimedPoint, false);
        objectHitEnd = Time.time + .23f;
    }

    private void UpdateWeaponClearance()
    {
        Transform eye = view.transform;
        aimedProp = null;
        if (Physics.Raycast(eye.position, eye.forward, out RaycastHit hit, 2.25f, ~0, QueryTriggerInteraction.Ignore))
        {
            aimedProp = hit.collider.GetComponentInParent<FacilityShootableProp>();
            aimedPoint = hit.point;
        }
        if (!aimedProp)
        {
            Vector3 reachOrigin = eye.position - Vector3.up * .57f + movement.Heading * .18f;
            if (Physics.SphereCast(reachOrigin, .24f, movement.Heading, out RaycastHit reach, 1.65f, ~0, QueryTriggerInteraction.Ignore))
            {
                aimedProp = reach.collider.GetComponentInParent<FacilityShootableProp>();
                if (aimedProp) aimedPoint = reach.point;
            }
        }

        // Check the shoulder and barrel sweep. Loose props remain shootable right up close.
        Vector3 origin = eye.position - eye.up * .14f + eye.right * .13f;
        int overlapCount = Physics.OverlapSphereNonAlloc(origin, .14f, clearanceOverlaps, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider shape = clearanceOverlaps[i];
            Vector3 separation = shape.ClosestPoint(origin) - origin;
            if (BlocksWeapon(shape) && (separation.sqrMagnitude < .0001f ||
                Mathf.Abs(Vector3.Dot(separation.normalized, Vector3.up)) < .72f))
            {
                wallClearance = 1;
                return;
            }
        }
        int count = Physics.SphereCastNonAlloc(origin, .14f, eye.forward, clearanceHits, 1.02f, ~0, QueryTriggerInteraction.Ignore);
        float nearest = 1.02f;
        for (int i = 0; i < count; i++)
        {
            RaycastHit sample = clearanceHits[i];
            if (!BlocksWeapon(sample.collider) || Mathf.Abs(Vector3.Dot(sample.normal, Vector3.up)) > .72f)
                continue;
            nearest = Mathf.Min(nearest, sample.distance);
        }
        wallClearance = 1 - Mathf.InverseLerp(.28f, 1.02f, nearest);
    }

    private bool BlocksWeapon(Collider shape) => shape.attachedRigidbody != playerBody &&
        !shape.GetComponentInParent<FacilityShootableProp>() && !shape.GetComponentInParent<FacilityCreature>();

    private void PlayFootsteps()
    {
        Vector3 velocity = playerBody.linearVelocity;
        if (Time.time < footstepAt || velocity.x * velocity.x + velocity.z * velocity.z < 2 ||
            !Physics.Raycast(playerBody.position, Vector3.down, out RaycastHit ground, 1.11f, ~0, QueryTriggerInteraction.Ignore))
            return;
        footstepAudio.pitch = Random.Range(.94f, 1.07f);
        footstepAudio.PlayOneShot(effects.Footstep(SurfaceUnderfoot(ground.collider)), movement.IsSprinting ? .48f : .33f);
        footstepAt = Time.time + (movement.IsSprinting ? .28f : .44f);
    }

    private StepSurface SurfaceUnderfoot(Collider contact)
    {
        var prop = contact.GetComponentInParent<FacilityShootableProp>();
        if (prop) return prop.FootstepSurface;
        Renderer renderer = contact.GetComponent<Renderer>();
        if (!renderer) renderer = contact.GetComponentInParent<Renderer>();
        Material material = renderer ? renderer.sharedMaterial : null;
        if (material == crateBoards || material == wood) return StepSurface.Wood;
        if (material == brushedSteel || material == corrugatedSteel || material == steel) return StepSurface.Steel;
        string label = material ? material.name.ToLowerInvariant() : string.Empty;
        if (label.Contains("wood") || label.Contains("pine") || label.Contains("pallet")) return StepSurface.Wood;
        if (label.Contains("steel") || label.Contains("metal") || label.Contains("barrel")) return StepSurface.Steel;
        if (label.Contains("plastic")) return StepSurface.Plastic;
        if (label.Contains("kraft") || label.Contains("cardboard")) return StepSurface.Cardboard;
        return StepSurface.Concrete;
    }

    private bool NearLift()
    {
        Vector3 callPosition = liftDoor.position;
        callPosition.y = Elevation + 1;
        return Near(callPosition);
    }

    private void Reload()
    {
        if (ammo == 30 || reserve == 0 || reloadEnd > 0) return;
        reloadEnd = Time.time + FacilityWeaponView.ReloadDuration;
        playerAudio.pitch = 1;
        playerAudio.PlayOneShot(effects.Reload, .65f);
    }

    private void Shoot()
    {
        ammo--;
        nextShot = Time.time + .105f;
        muzzleEnd = Time.time + .038f;
        distractionEnd = Time.time + 1.65f;
        noiseEnd = Time.time + 2.2f;
        noisePosition = view.transform.position;
        weaponView.Kick();
        playerAudio.pitch = Random.Range(.91f, 1.09f);
        playerAudio.PlayOneShot(effects.Shot, .57f);
        roomAudio.PlayOneShot(effects.RoomTail(floor), .46f);
        Vector3 direction = view.transform.forward;
        Vector3 origin = view.transform.position + direction * .17f;
        if (!Physics.Raycast(origin, direction, out RaycastHit impact, 80, ~0, QueryTriggerInteraction.Ignore))
            return;
        FacilityCreature enemy = impact.collider.GetComponentInParent<FacilityCreature>();
        if (enemy && enemy.Alive)
        {
            enemy.TakeHit(24);
            hitEnd = Time.time + .11f;
        }
        if (impact.collider.GetComponentInParent<FacilityShootableLight>() is FacilityShootableLight lamp)
        {
            lamp.Hit(effects, impact.point, impact.normal);
            objectHitEnd = Time.time + .24f;
        }
        else if (impact.collider.GetComponentInParent<FacilityShootableProp>() is FacilityShootableProp prop)
        {
            prop.Hit(effects, impact.point, impact.normal, direction);
            objectHitEnd = Time.time + .18f;
        }
        else
            effects.Impact(impact.point, impact.normal, enemy ? new Color(.39f, .055f, .035f) : new Color(1, .74f, .38f));
    }

    private void Say(string text)
    {
        message = text;
        messageEnd = Time.time + 3.8f;
    }

}
