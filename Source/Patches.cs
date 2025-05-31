using HarmonyLib;
using UnityEngine;

namespace ExampleMod;

[HarmonyPatch]
public class Patches {
    [HarmonyPatch(typeof(Debug), nameof(Debug.Log), typeof(object))]
    [HarmonyPrefix]
    private static bool Nope() => false;
}