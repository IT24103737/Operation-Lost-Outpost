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
    private Transform head, tail;
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
        Part(rig, "Distended ribcage", new Vector3(0, 1.68f, 0), new Vector3(1.1f, 1.31f, .83f), skin, -11);
        Part(rig, "Pelvis", new Vector3(0, 1.19f, -.11f), new Vector3(.85f, .64f, .71f), skin);
        head = Joint("Head pivot", rig, new Vector3(.085f, 2.18f, .39f));
        Part(head, "Skull", Vector3.zero, new Vector3(.51f, .55f, .51f), skin, -18);
        Part(head, "Jaw", new Vector3(0, -.17f, .14f), new Vector3(.39f, .19f, .35f), effects.Dark);
        Part(head, "Left eye", new Vector3(-.118f, .047f, .234f), Vector3.one * .071f, effects.Eye);
        Part(head, "Right eye", new Vector3(.118f, .057f, .234f), Vector3.one * .065f, effects.Eye);
        for (int sign = -1; sign < 2; sign += 2)
        {
            float mass = sign < 0 ? 1.18f : .89f;
            Transform arm = Joint("Shoulder", rig, new Vector3(sign * .53f, 1.97f, 0));
            limbs.Add(arm);
            Part(arm, "Overgrown upper arm", new Vector3(sign * .16f, -.33f, 0), new Vector3(.53f * mass, .94f, .54f * mass), skin, sign * 21);
            Part(arm, "Armoured forearm", new Vector3(sign * .26f, -.78f, .095f), new Vector3(.46f * mass, .76f, .56f * mass), skin, -sign * 12);
            Part(arm, "Knuckle", new Vector3(sign * .21f, -1.09f, .17f), new Vector3(.34f * mass, .27f, .35f), effects.Bone);
            Transform leg = Joint("Hip", rig, new Vector3(sign * .27f, 1.13f, -.09f));
            limbs.Add(leg);
            Part(leg, "Thigh", new Vector3(0, -.26f, .04f), new Vector3(.44f, .66f, .53f), skin, -sign * 7);
            Part(leg, "Shin", new Vector3(sign * .025f, -.78f, -.01f), new Vector3(.34f, .66f, .38f), skin);
            Part(leg, "Foot", new Vector3(0, -1.025f, .14f), new Vector3(.35f, .23f, .59f), effects.Dark);
        }
        for (int i = 0; i < 6; i++)
            Part(rig, "Calcified spine", new Vector3(Mathf.Sin(i * 4) * .28f, 1.59f + i * .13f, -.42f + i * .027f),
                new Vector3(.21f, .31f, .28f), i % 2 == 0 ? effects.Bone : skin, i * 13);
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

