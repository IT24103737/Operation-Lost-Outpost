using System.Collections.Generic;
using UnityEngine;
using Species = FacilityEncounter.Species;

internal sealed class FacilityCreatureSkin
{
    private readonly Transform rig;
    private readonly List<Transform> limbs = new();
    private readonly List<MeshRenderer> fleshRenderers = new();
    private readonly MaterialPropertyBlock tint = new();
    private readonly FacilityEffects effects;
    private readonly Species species;
    private Transform head, tail, jaw, chest;
    internal enum BruteAction { Idle, Roar, ChargeWindup, Charge, MeleeWindup, Melee, Recover, Stunned }
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    internal FacilityCreatureSkin(Transform root, Species kind, FacilityEffects art)
    {
        effects = art;
        species = kind;
        rig = Joint("Creature rig", root, Vector3.zero);
        if (kind == Species.Goliath) MakeBrute();
        else MakeCrawler();
    }

    private static Transform Joint(string name, Transform parent, Vector3 position)
    {
        Transform joint = new GameObject(name).transform;
        joint.SetParent(parent, false);
        joint.localPosition = position;
        return joint;
    }

    private Transform Part(Transform parent, string name, Vector3 center, Vector3 size, Material material, float roll = 0)
    {
        Transform piece = Joint(name, parent, center);
        piece.localScale = size;
        piece.localRotation = Quaternion.Euler(0, 0, roll);
        piece.gameObject.AddComponent<MeshFilter>().sharedMesh = effects.Flesh;
        MeshRenderer renderer = piece.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        fleshRenderers.Add(renderer);
        return piece;
    }

    private void MakeBrute()
    {
        Material skin = effects.Skin(species);
        chest = Part(rig, "Heavy ribcage", new Vector3(0, 1.86f, -.08f), new Vector3(1.30f, 1.40f, .90f), skin, -6);
        Part(rig, "Abdominal muscle", new Vector3(0, 1.32f, .12f), new Vector3(.84f, .82f, .69f), skin);
        Part(rig, "Pelvis", new Vector3(0, 1.15f, -.09f), new Vector3(.88f, .57f, .73f), skin);
        Part(rig, "Raised trapezius", new Vector3(0, 2.27f, -.19f), new Vector3(1.16f, .71f, .73f), skin);
        head = Joint("Head pivot", rig, new Vector3(.04f, 2.49f, .36f));
        Part(head, "Recessed skull", Vector3.zero, new Vector3(.46f, .55f, .48f), skin, -8);
        Part(head, "Heavy brow", new Vector3(0, .105f, .21f), new Vector3(.50f, .10f, .17f), effects.Dark, -7);
        jaw = Part(head, "Split lower jaw", new Vector3(0, -.18f, .15f), new Vector3(.40f, .24f, .34f), effects.Dark);
        for (int side = -1; side <= 1; side += 2)
        {
            Part(head, "Burning inset eye", new Vector3(side * .12f, .035f, .252f),
                new Vector3(.070f, .045f, .04f), effects.Eye);
            for (int tooth = 0; tooth < 3; tooth++)
                Part(jaw, "Exposed tooth", new Vector3(side * (.06f + tooth * .06f), .11f, .11f),
                    new Vector3(.036f, .105f - tooth * .015f, .048f), effects.Bone, side * 8);
            float mass = side < 0 ? 1.2f : .95f;
            Transform arm = Joint("Massive shoulder", rig, new Vector3(side * .64f, 2.18f, -.02f));
            limbs.Add(arm);
            Part(arm, "Deltoid", new Vector3(side * .07f, -.1f, 0), new Vector3(.69f * mass, .65f, .72f), skin);
            Part(arm, "Overgrown upper arm", new Vector3(side * .15f, -.38f, 0),
                new Vector3(.54f * mass, .80f, .59f * mass), skin, side * 13);
            Part(arm, "Armoured forearm", new Vector3(side * .21f, -.88f, .13f),
                new Vector3(.52f * mass, .86f, .64f * mass), skin, -side * 9);
            Part(arm, "Calcified striking plate", new Vector3(side * .23f, -.94f, .39f),
                new Vector3(.40f * mass, .56f, .18f), effects.Bone, -side * 9);
            Part(arm, "Massive fist", new Vector3(side * .19f, -1.24f, .18f),
                new Vector3(.46f * mass, .34f, .46f), skin);
            for (int finger = 0; finger < 3; finger++)
                Part(arm, "Knuckle spur", new Vector3(side * .19f + (finger - 1) * .12f, -1.30f, .39f),
                    new Vector3(.085f, .18f, .13f), effects.Bone, (finger - 1) * 16);
            for (int spike = 0; spike < 3; spike++)
                Part(arm, "Broken shoulder spike", new Vector3(side * (.10f + spike * .16f), .16f, -.07f),
                    new Vector3(.14f, .36f - spike * .06f, .17f), effects.Bone, -side * (25 + spike * 17));

            Transform leg = Joint("Hip", rig, new Vector3(side * .29f, 1.12f, -.09f));
            limbs.Add(leg);
            Part(leg, "Weight-bearing thigh", new Vector3(0, -.27f, .03f), new Vector3(.50f, .69f, .58f), skin, -side * 6);
            Part(leg, "Armoured knee", new Vector3(0, -.57f, .22f), new Vector3(.31f, .28f, .20f), effects.Bone);
            Part(leg, "Shin", new Vector3(side * .025f, -.78f, -.01f), new Vector3(.37f, .64f, .41f), skin);
            Part(leg, "Planted foot", new Vector3(0, -1.015f, .15f), new Vector3(.44f, .23f, .63f), effects.Dark);
            for (int toe = 0; toe < 3; toe++)
                Part(leg, "Toe claw", new Vector3((toe - 1) * .12f, -1.04f, .40f),
                    new Vector3(.085f, .12f, .20f), effects.Bone);
        }
        for (int i = 0; i < 7; i++)
            Part(rig, "Exposed vertebral armour", new Vector3(0, 1.45f + i * .17f, -.44f),
                new Vector3(.29f, .30f, .24f + i * .025f), effects.Bone, i % 2 == 0 ? 7 : -7);
        for (int i = 0; i < 4; i++)
            Part(rig, "Torn rib armour", new Vector3(-.23f + i * .13f, 1.79f + i * .12f, .36f),
                new Vector3(.09f, .33f, .12f), effects.Bone, -34);
        Part(rig, "Broken restraint harness", new Vector3(.32f, 1.85f, .32f),
            new Vector3(.13f, 1.05f, .12f), effects.Dark, 23);
    }

    internal void AnimateBrute(float phase, float motion, BruteAction action, float progress, float impact, float dead, bool enraged)
    {
        float breath = Mathf.Sin(phase * (enraged ? 1.7f : .85f));
        float gait = Mathf.Sin(phase) * motion;
        float lean = 6 + motion * 9;
        float armLeft = gait * 21, armRight = -gait * 21;
        float armSpread = 7;
        float jawOpen = .01f;
        float headPitch = -4;
        switch (action)
        {
            case BruteAction.Roar:
                armLeft = armRight = -35;
                armSpread = 35 * Mathf.Sin(progress * Mathf.PI);
                headPitch = -22;
                jawOpen = .07f * Mathf.Sin(progress * Mathf.PI);
                lean = -6;
                break;
            case BruteAction.ChargeWindup:
                lean = Mathf.Lerp(-8, 20, progress);
                armLeft = armRight = -40;
                headPitch = -12;
                break;
            case BruteAction.Charge:
                lean = 22;
                armLeft = -54 + gait * 8;
                armRight = -28 - gait * 8;
                headPitch = -18;
                jawOpen = .035f;
                break;
            case BruteAction.MeleeWindup:
                armLeft = Mathf.Lerp(-20, -152, progress);
                armRight = 12;
                lean = -9 * progress;
                headPitch = -10;
                break;
            case BruteAction.Melee:
                armLeft = Mathf.Lerp(-152, 18, Mathf.SmoothStep(0, 1, progress));
                armRight = -22;
                lean = 10 + 14 * Mathf.Sin(progress * Mathf.PI);
                jawOpen = .04f;
                break;
            case BruteAction.Recover:
                armLeft = 18 * (1 - progress);
                lean = 12 * (1 - progress);
                break;
            case BruteAction.Stunned:
                armLeft = armRight = 8;
                lean = 15;
                headPitch = 20 + Mathf.Sin(phase * 2) * 5;
                break;
        }
        limbs[0].localRotation = Quaternion.Euler(armLeft, 0, armSpread);
        limbs[2].localRotation = Quaternion.Euler(armRight, 0, -armSpread);
        limbs[1].localRotation = Quaternion.Euler(-gait * 25, 0, 3);
        limbs[3].localRotation = Quaternion.Euler(gait * 25, 0, -3);
        chest.localScale = new Vector3(1.30f, 1.40f, .90f) * (1 + breath * .012f);
        jaw.localPosition = new Vector3(0, -.18f - jawOpen, .15f);
        head.localRotation = Quaternion.Euler(headPitch + impact * 9, breath * 3, 0);
        rig.localPosition = new Vector3(0, .016f * breath + Mathf.Abs(gait) * .025f - dead * .38f, 0);
        rig.localRotation = Quaternion.Euler(lean + impact * 4, 0, dead * 88 + gait * 2);
        Color skinColor = enraged ? new Color(1, .66f, .57f) : Color.white;
        tint.SetColor(BaseColor, Color.Lerp(skinColor, new Color(1, .38f, .32f), impact * .6f));
        foreach (MeshRenderer renderer in fleshRenderers)
            if (renderer.sharedMaterial == effects.Skin(species)) renderer.SetPropertyBlock(tint);
    }

    private void MakeCrawler()
    {
        Material skin = effects.Skin(species);
        float scale = species == Species.Swarmer ? .45f : 1;
        bool leech = species == Species.Leech;
        Transform frame = Joint("Body frame", rig, Vector3.zero);
        frame.localScale *= scale;
        Part(frame, "Ribcage", new Vector3(0, .53f, -.01f), new Vector3(leech ? .74f : .76f, .51f, 1.12f), skin);
        Part(frame, "Haunches", new Vector3(0, .43f, -.46f), new Vector3(.61f, .49f, .55f), skin);
        head = Joint("Head pivot", frame, new Vector3(0, .5f, .47f));
        Part(head, "Skull", Vector3.zero, new Vector3(.52f, .4f, leech ? .48f : .63f), skin);
        Part(head, leech ? "Feeding mouth" : "Snout", new Vector3(0, -.08f, .28f), new Vector3(.31f, .2f, .38f), effects.Dark);
        for (int sign = -1; sign < 2; sign += 2)
        {
            Part(head, "Eye", new Vector3(sign * .185f, .064f, .187f), new Vector3(.10f, .09f, .097f), effects.Eye);
            if (species == Species.Swarmer)
                Part(head, "Torn ear", new Vector3(sign * .202f, .17f, -.11f), new Vector3(.18f, .24f, .105f), effects.Dark, -sign * 34);
            int pairs = leech ? 3 : 2;
            for (int i = 0; i < pairs; i++)
            {
                Transform limb = Joint("Limb pivot", frame, new Vector3(sign * .25f, .42f, .28f - .5f * i));
                limbs.Add(limb);
                Part(limb, "Upper limb", new Vector3(sign * .16f, -.035f, .03f), new Vector3(.43f, .17f, .20f), skin, sign * 19);
                Part(limb, "Lower limb", new Vector3(sign * .32f, -.19f, .095f), new Vector3(.13f, .4f, .16f), skin, -sign * 19);
                Part(limb, "Claw", new Vector3(sign * .35f, -.36f, .15f), new Vector3(.15f, .105f, .22f), effects.Bone);
            }
        }
        tail = Joint("Tail pivot", frame, new Vector3(0, .38f, -.6f));
        for (int i = 0; i < 5; i++)
            Part(tail, "Tail segment", new Vector3(Mathf.Sin(i * .37f) * .13f, -i * .039f, -i * .16f),
                new Vector3(.25f - i * .038f, .22f - i * .03f, .27f), i > 1 ? effects.Dark : skin);
        if (leech)
        {
            for (int i = 0; i < 5; i++)
                Part(frame, "Conductive dorsal organ", new Vector3(0, .78f - .043f * i, .32f - i * .2f),
                    new Vector3(.25f - i * .024f, .13f, .16f), effects.Eye);
        }
        if (species == Species.Stalker)
        {
            for (int i = 0; i < 7; i++)
                Part(frame, "Dorsal scale", new Vector3(0, .83f - i * .035f, .29f - i * .154f),
                    new Vector3(.15f, .21f, .18f), effects.Bone, i * 14);
        }
    }

    internal void Animate(float phase, float motion, float windup, float impact, float dead, bool hidden, bool upsideDown)
    {
        float direction = upsideDown ? -1 : 1;
        float step = 33 * motion;
        for (int i = 0; i < limbs.Count; i++)
        {
            float swing = Mathf.Sin(phase + i * (species == Species.Goliath ? Mathf.PI * .5f : Mathf.PI)) * step;
            limbs[i].localRotation = Quaternion.Euler(swing, 0, species == Species.Goliath ? windup * (i < 2 ? 18 : -18) : 0);
        }
        if (tail) tail.localRotation = Quaternion.Euler(Mathf.Sin(phase * .53f) * 5, Mathf.Sin(phase) * 23, 0);
        head.localRotation = Quaternion.Euler(-windup * 26 + impact * 12, Mathf.Sin(phase * .38f) * 5, 0);
        rig.localPosition = new Vector3(0, (upsideDown ? .85f : 0) + direction * Mathf.Sin(phase * 2) * (.02f + .027f * motion), 0);
        rig.localRotation = Quaternion.Euler(-windup * 17 + impact * 7, 0, (upsideDown ? 180 : 0) + dead * 86);
        tint.SetColor(BaseColor, hidden ? new Color(.19f, .21f, .21f) : Color.Lerp(Color.white, new Color(.85f, .38f, .32f), impact));
        foreach (MeshRenderer render in fleshRenderers)
        {
            if (render.sharedMaterial == effects.Skin(species))
                render.SetPropertyBlock(tint);
            else
                render.enabled = !hidden;
        }
    }
}

