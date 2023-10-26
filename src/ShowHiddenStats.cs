using Bundles;
using Game;
using Game.UI;
using Game.Units;
using HarmonyLib;
using Modding;
using Munitions;
using Ships;
using Ships.Controls;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace ShowHiddenStats
{
    public class ShowHiddenStats : IModEntryPoint
    {
        public void PostLoad()
        {
            Harmony harmony = new Harmony("nebulous.show-hidden-stats");
            harmony.PatchAll();
        }

        public void PreLoad()
        {

        }

        public static void AddArmorShreddingStat(ref string __result, float armorDamageRadius)
        {
            if (armorDamageRadius > 0)
            {
                __result = __result + "\n" + "Armor Shredding Radius: " + armorDamageRadius + " m";
            }
        }

        public static void AddOverpenDamageStat(ref string __result, float overpenDamageMultiplier)
        {
            if (overpenDamageMultiplier < 1)
            {
                __result = __result + "\n" + "Overpenetration Damage: " + (overpenDamageMultiplier * 100) + " %";
            }
        }

        public static void AddCriticalEventStat(ref string __result, float randomEffectMultiplier)
        {
            if (randomEffectMultiplier != 1f)
            {
                randomEffectMultiplier = (randomEffectMultiplier - 1) * 100;
                string prefix = (randomEffectMultiplier < 0) ? "" : "+";
                __result = __result + "\n" + "Critical Event Chance: " + prefix + randomEffectMultiplier + " %";
            }
        }

        public static void AddCrewDamageStat(ref string __result, float crewVulnerabilityMultiplier)
        {
            if (crewVulnerabilityMultiplier != 1f)
            {
                crewVulnerabilityMultiplier = (crewVulnerabilityMultiplier - 1) * 100;
                string prefix = (crewVulnerabilityMultiplier < 0) ? "" : "+";
                __result = __result + "\n" + "Crew Damage Modifier: " + prefix + crewVulnerabilityMultiplier + " %";
            }
        }

        public static void AddDepthFalloffStat(ref string result, AnimationCurve depthFalloff, float maxDepth, float maxRange)
        {
            if (depthFalloff == null) return;

            result = result + "\n\n";
            result = result + "Penetration Depth at Range:";

            float[] distances = { 0.25f, 0.50f, 0.75f, 1.00f };

            foreach (float distance in distances)
            {
                result = result + "\n";
                float depth = depthFalloff.Evaluate(distance) * maxDepth * 10f;
                result = result + string.Format("   * {0:N0} m at {1:0.##} km", depth, distance * maxRange * 10f / 1000f);
            }
        }

        public static void AddDamageFalloffStat(ref string result, AnimationCurve damageFalloff, float maxRange)
        {
            if (damageFalloff == null) return;

            result = result + "\n\n";
            result = result + "Damage and Penetration at Range:";

            float[] distances = { maxRange - 300f, maxRange - 100f, maxRange };

            foreach (float distance in distances)
            {
                result = result + "\n";
                float damage = damageFalloff.Evaluate(distance / maxRange) * 100;
                result = result + string.Format("   * {0:N0}% at {1:0.##} km", damage, distance * 10f / 1000f);
            }
        }

        public static float calculateRoundsPerMinute(DiscreteWeaponComponent weapon, bool useBaseStats = false)
        {
            int magazineSize = (int)Utilities.GetPrivateField(weapon, "_magazineSize");
            StatValue statRecycleTime = (StatValue)Utilities.GetPrivateField(weapon, "_statRecycleTime");
            StatValue statReloadTime = (StatValue)Utilities.GetPrivateField(weapon, "_statReloadTime");

            float fullCycleTime;

            if (useBaseStats)
            {
                fullCycleTime = ((magazineSize - 1) * statRecycleTime.BaseValue) + statReloadTime.BaseValue;
            }
            else
            {
                fullCycleTime = ((magazineSize - 1) * statRecycleTime.Value) + statReloadTime.Value;
            }

            float secondsPerRound = fullCycleTime / magazineSize;
            float roundsPerMinute = 60 / secondsPerRound;

            return roundsPerMinute;
        }

        public static float calculateRoundsPerMinute(ContinuousWeaponComponent weapon)
        {
            Muzzle[] muzzles = (Muzzle[])Utilities.GetPrivateField(weapon, "_muzzles");

            float roundsPerMinute = 0;

            foreach (Muzzle muzzle in muzzles)
            {
                if (muzzle is IFireRateMuzzle rofMuzzle)
                {
                    roundsPerMinute += rofMuzzle.RoundsPerMinute;
                }
            }

            return roundsPerMinute;
        }

        public static float[] findThrusterStrengthValues(BaseHull hull)
        {
            float thrusterValueRear = 0.0f;
            float thrusterValueFore = 0.0f;
            float thrusterValueSide = 0.0f;
            
            int numThrustersRear = 0;
            int numThrustersFore = 0;
            int numThrustersSide = 0;

            foreach (HullPart subpart in hull.AllSubParts)
            {
                if (subpart.GetType() == typeof(ThrusterPart))
                {
                    ThrusterPart thruster = (ThrusterPart)subpart;

                    bool isMainEngine = (bool)Utilities.GetPrivateField(thruster, "_mainEngine");
                    bool isManeuvering = (bool)Utilities.GetPrivateField(thruster, "_contributeAngular");

                    if (isMainEngine)
                    {
                        numThrustersRear++;
                        thrusterValueRear += (float)Utilities.GetPrivateField(thruster, "_power");
                    }
                    else if (!isMainEngine && !isManeuvering)
                    {
                        numThrustersFore++;
                        thrusterValueFore += (float)Utilities.GetPrivateField(thruster, "_power");
                    }
                    else
                    {
                        numThrustersSide++;
                        thrusterValueSide += (float)Utilities.GetPrivateField(thruster, "_power");
                    }
                }
            }

            float[] thrusterStrengths = { thrusterValueRear / numThrustersRear, thrusterValueFore / numThrustersFore, thrusterValueSide / numThrustersSide  };
            return thrusterStrengths;
        }
    }
    
    [HarmonyPatch(typeof(BaseHull), "EditorFormatHullStats")]
    class Patch_BaseHull_EditorFormatHullStats
    {
        static void Postfix(ref BaseHull __instance, ref string __result)
        {
            StatValue statInternalDensity = (StatValue)Utilities.GetPrivateField(__instance, "_statInternalDensity");

            string internalDensity = "\n" + "Internal Density: " + statInternalDensity.Value + " " + statInternalDensity.Unit;

            int densityInsertionPoint = __result.IndexOf("Component DR") - 1;
            __result = __result.Insert(densityInsertionPoint, internalDensity);

            StatValue statIdentityWorkRequired = (StatValue)Utilities.GetPrivateField(__instance, "_statIdentityWorkRequired");

            string intelSummary = "";
            intelSummary = intelSummary + "Time Unidentified vs Basic CIC: " + string.Format("{0:0} seconds", statIdentityWorkRequired.Value / 1) + "\n";
            intelSummary = intelSummary + "Time Unidentified vs Citadel CIC: " + string.Format("{0:0} seconds", statIdentityWorkRequired.Value / 4) + "\n";
            intelSummary = intelSummary + "Time Unidentified vs Intel Centre: " + string.Format("{0:0} seconds", statIdentityWorkRequired.Value / 15) + "\n";

            int intelInsertionPoint = __result.IndexOf("Signatures") - 1;
            __result = __result.Insert(intelInsertionPoint, intelSummary);
        }
    }
    
    [HarmonyPatch(typeof(BaseHull), "EditorFormatPropulsionStats")]
    class Patch_BaseHull_EditorFormatPropulsionStats
    {
        static void Postfix(ref BaseHull __instance, ref string __result)
        {
            List<DriveComponent> propulsionComponents = __instance.CollectComponents<DriveComponent>();
            if (propulsionComponents != null && propulsionComponents.Count > 0)
            {
                string thrusterPowerString = "";
                float[] thrusterStrengthValues = ShowHiddenStats.findThrusterStrengthValues(__instance);

                thrusterPowerString = thrusterPowerString + string.Format(" - Main Thrusters: {0:N0}%", thrusterStrengthValues[0] * 100);
                thrusterPowerString = thrusterPowerString + "\n";

                thrusterPowerString = thrusterPowerString + string.Format(" - Fore Thrusters: {0:N0}%", thrusterStrengthValues[1] * 100);
                thrusterPowerString = thrusterPowerString + "\n";

                thrusterPowerString = thrusterPowerString + string.Format(" - Side Thrusters: {0:N0}%", thrusterStrengthValues[2] * 100);
                thrusterPowerString = thrusterPowerString + "\n";

                __result = __result.Replace("Acceleration Time", thrusterPowerString + "Acceleration Time");

                StatValue statAngularMotor = (StatValue)Utilities.GetPrivateField(__instance, "_statAngularMotor");
                StatValue statMaxTurnSpeed = (StatValue)Utilities.GetPrivateField(__instance, "_statMaxTurnSpeed");

                Vector3D tensor = Vector3D.Zero;
                Vector3 extents = __instance.TensorCalculationDimensions;
                double hullMass = (double)__instance.Mass;
                tensor.X = 0.08333333333333333 * hullMass * (double)(extents.y * extents.y + extents.z * extents.z);
                tensor.Y = 0.08333333333333333 * hullMass * (double)(extents.x * extents.x + extents.z * extents.z);
                tensor.Z = 0.08333333333333333 * hullMass * (double)(extents.x * extents.x + extents.y * extents.y);

                double momentInertiaY = tensor.Y;
                double momentInertiaZ = tensor.Z;
                double angularDamping = 0.1;

                double baseTrueMaxTurnRate = Math.Min(statAngularMotor.BaseValue / momentInertiaY / angularDamping * 180 / Math.PI, statMaxTurnSpeed.BaseValue * 180 / Math.PI);
                double finalTrueMaxTurnRate = Math.Min(statAngularMotor.Value / momentInertiaY / angularDamping * 180 / Math.PI, statMaxTurnSpeed.Value * 180 / Math.PI);
                float modifierTrueMaxTurnRate = (float)(finalTrueMaxTurnRate / baseTrueMaxTurnRate) - 1.0f;

                double baseMax180TurnSpeed = 180 / (statMaxTurnSpeed.BaseValue * 180 / Math.PI);
                double finalMax180TurnSpeed = 180 / (statMaxTurnSpeed.Value * 180 / Math.PI);

                double baseTimeToTurn180 = Math.Max((momentInertiaY * angularDamping * Math.PI / statAngularMotor.BaseValue) + 10, baseMax180TurnSpeed);
                double finalTimeToTurn180 = Math.Max((momentInertiaY * angularDamping * Math.PI / statAngularMotor.Value) + 10, finalMax180TurnSpeed);
                float modifierTimeToTurn180 = (float)(finalTimeToTurn180 / baseTimeToTurn180) - 1.0f;

                double baseTimeToRoll180 = Math.Max((momentInertiaZ * angularDamping * Math.PI / statAngularMotor.BaseValue) + 10, baseMax180TurnSpeed);
                double finalTimeToRoll180 = Math.Max((momentInertiaZ * angularDamping * Math.PI / statAngularMotor.Value) + 10, finalMax180TurnSpeed);
                float modifierTimeToRoll180 = (float)(finalTimeToRoll180 / baseTimeToRoll180) - 1.0f;

                __result = __result.Replace("Turn Rate", "Theoretical Turn Rate");

                __result = __result + "\n";
                __result = __result + string.Format("Actual Max Turn Rate: {0:0.##} deg/s", finalTrueMaxTurnRate);
                if (modifierTrueMaxTurnRate != 0.0f) __result = __result + " (" + StatModifier.FormatModifierColored(modifierTrueMaxTurnRate, false) + ")";

                __result = __result + "\n";
                __result = __result + string.Format("Estimated 180 Turn: {0:0} seconds", finalTimeToTurn180);
                if (modifierTimeToTurn180 != 0.0f) __result = __result + " (" + StatModifier.FormatModifierColored(modifierTimeToTurn180, true) + ")";

                __result = __result + "\n";
                __result = __result + string.Format("Estimated 180 Roll: {0:0} seconds", finalTimeToRoll180);
                if (modifierTimeToRoll180 != 0.0f) __result = __result + " (" + StatModifier.FormatModifierColored(modifierTimeToRoll180, true) + ")";
            }
        }
    }

    [HarmonyPatch(typeof(BaseHull), "EditorFormatWeaponStats")]
    class Patch_BaseHull_EditorFormatWeaponStats
    {
        static void Postfix(ref BaseHull __instance, ref string __result)
        {
            List<IWeapon> weapons = __instance.CollectComponents<IWeapon>();

            //Debug.Log("SHS AMMOSUMMARY start");

            if (weapons.Count == 0) return;
            
            string headerText = string.Concat(new string[]
            {
                "<color=",
                GameColors.YellowTextColor,
                "><b>",
                "Calculated Firing Time",
                "</b></color>",
                "\n"
            });

            string ammoSummaryText = headerText;

            bool anyMunitions = false;

            //Debug.Log("SHS AMMOSUMMARY pre-foreach");

            foreach (IMunition munition in BundleManager.Instance.AllMunitions)
            {
                //Debug.Log("SHS AMMOSUMMARY munition: " + munition.MunitionName);

                int numLoaded = __instance.MyShip.CountAmmoType(munition);
                
                if (numLoaded > 0)
                {
                    //Debug.Log("SHS AMMOSUMMARY munition loaded");

                    float totalFireRate = 0;

                    foreach (IWeapon weapon in weapons)
                    {
                        //Debug.Log("SHS AMMOSUMMARY weapon: " + weapon.WepName);

                        if (weapon.IsAmmoCompatible(munition))
                        {
                            if (weapon is DiscreteWeaponComponent discreteWeapon)
                            {
                                totalFireRate += ShowHiddenStats.calculateRoundsPerMinute(discreteWeapon);
                            }
                            if (weapon is ContinuousWeaponComponent continuousWeapon)
                            {
                                totalFireRate += ShowHiddenStats.calculateRoundsPerMinute(continuousWeapon);
                            }
                        }
                    }

                    //Debug.Log("SHS AMMOSUMMARY rpm: " + totalFireRate);

                    if (totalFireRate > 0)
                    {
                        ammoSummaryText = ammoSummaryText + munition.MunitionName + ": " + string.Format("{0:0} minutes", numLoaded / totalFireRate);
                        ammoSummaryText = ammoSummaryText + "\n";

                        anyMunitions = true;
                    }
                }
            }

            //Debug.Log("SHS AMMOSUMMARY aft-foreach");

            ammoSummaryText = ammoSummaryText + "\n";

            if (anyMunitions)
            {
                __result = ammoSummaryText + __result;
            }
        }
    }

    [HarmonyPatch(typeof(LightweightMunitionBase), "GetDetailText")]
    class Patch_LightweightMunitionBase_GetDetailText
    {
        static void Postfix(ref LightweightMunitionBase __instance, ref string __result)
        {
            float armorDamageRadius = (float)Utilities.GetPrivateField(__instance, "_armorDamageRadius");
            float randomEffectMultiplier = (float)Utilities.GetPrivateField(__instance, "_randomEffectMultiplier");
            float overpenDamageMultiplier = (float)Utilities.GetPrivateField(__instance, "_overpenDamageMultiplier");
            float crewVulnerabilityMultiplier = (float)Utilities.GetPrivateField(__instance, "_crewVulnerabilityMultiplier");

            ShowHiddenStats.AddArmorShreddingStat(ref __result, armorDamageRadius);
            ShowHiddenStats.AddOverpenDamageStat(ref __result, overpenDamageMultiplier);
            ShowHiddenStats.AddCriticalEventStat(ref __result, randomEffectMultiplier);
            ShowHiddenStats.AddCrewDamageStat(ref __result, crewVulnerabilityMultiplier);
        }
    }

    [HarmonyPatch(typeof(ContinuousWeaponComponent), "GetFormattedStats")]
    class Patch_ContinuousWeaponComponent_GetFormattedStats
    {
        static void Postfix(ref ContinuousWeaponComponent __instance, ref string __result)
        {
            Muzzle[] muzzles = (Muzzle[])Utilities.GetPrivateField(__instance, "_muzzles");

            if (muzzles.Length < 1 || muzzles[0].GetType() != typeof(ContinuousRaycastMuzzle)) return;

            float armorDamageEffectSize = (float)Utilities.GetPrivateField(muzzles[0], "_armorDamageEffectSize");
            float randomEffectMultiplier = (float)Utilities.GetPrivateField(muzzles[0], "_randomEffectMultiplier");
            float crewVulnerabilityMultiplier = (float)Utilities.GetPrivateField(muzzles[0], "_crewVulnerabilityMultiplier");
            AnimationCurve damageFalloff = (AnimationCurve)Utilities.GetPrivateField(muzzles[0], "_powerFalloff");
            float maxRange = (float)Utilities.GetPrivateField(muzzles[0], "_raycastRange");

            ShowHiddenStats.AddArmorShreddingStat(ref __result, armorDamageEffectSize);
            ShowHiddenStats.AddCriticalEventStat(ref __result, randomEffectMultiplier);
            ShowHiddenStats.AddCrewDamageStat(ref __result, crewVulnerabilityMultiplier);

            ShowHiddenStats.AddDamageFalloffStat(ref __result, damageFalloff, maxRange);

            __result = __result + "\n\n";
        }
    }

    [HarmonyPatch(typeof(DiscreteWeaponComponent), "GetFormattedStats")]
    class Patch_DiscreteWeaponComponent_GetFormattedStats
    {
        static void Postfix(ref DiscreteWeaponComponent __instance, ref string __result, ref int groupSize)
        {
            float baseRoundsPerMinute = ShowHiddenStats.calculateRoundsPerMinute(__instance, true);
            float finalRoundsPerMinute = ShowHiddenStats.calculateRoundsPerMinute(__instance);

            __result = __result + "Rate of Fire";
            if (groupSize > 1) __result = __result + " (" + groupSize + "x)";
            __result = __result + ": ";

            __result = __result + string.Format("{0:0.##} RPM", finalRoundsPerMinute * groupSize);

            float modifier = (finalRoundsPerMinute / baseRoundsPerMinute) - 1.0f;
            if (modifier != 0.0f) __result = __result + " (" + StatModifier.FormatModifierColored(modifier, false) + ")";

            __result = __result + "\n";
        }
    }

    [HarmonyPatch(typeof(BaseActiveSensorComponent), "GetFormattedStats")]
    class Patch_BaseActiveSensorComponent_GetFormattedStats
    {
        static void Postfix(ref BaseActiveSensorComponent __instance, ref string __result)
        {
            StatValue statMaxRange = (StatValue)Utilities.GetPrivateField(__instance, "_statMaxRange");
            StatValue statRadiatedPower = (StatValue)Utilities.GetPrivateField(__instance, "_statRadiatedPower");
            StatValue statAperture = (StatValue)Utilities.GetPrivateField(__instance, "_statAperture");
            StatValue statGain = (StatValue)Utilities.GetPrivateField(__instance, "_statGain");
            StatValue statSensitivity = (StatValue)Utilities.GetPrivateField(__instance, "_statSensitivity");

            __result = __result + "Detected by ELINT: " + string.Format("{0:0.## km}", statMaxRange.Value * 1.25f / 100f) + "\n";
            __result = __result + "Effective Power: " + string.Format("{0:0.## GW}", statRadiatedPower.Value * statAperture.Value * statGain.Value / 1000000f) + "\n";
            
            float[] signatures = { 1000, 2000, 3000, 5000, 7000, 9000, 12000 };

            foreach (float signature in signatures)
            {
                __result = __result + "\n";

                // = 100 * (power * gain^2 * aperture * signature / (4*PI())^2 / noise / 100)^0.25
                double rangeToPowerLimit4 = signature * statGain.Value * statGain.Value * statRadiatedPower.Value * statAperture.Value / (16 * Math.PI * Math.PI) / 100f;
                double rangeToPowerLimit = Math.Pow(rangeToPowerLimit4, (double)1 / 4) * 100f;

                // = 10 * (power * gain^2 * aperture * signature * 0.000625 / PI()^2 / 10^(sensitivity / 10))^0.25
                double rangeToSensitivityLimit4 = signature * statGain.Value * statGain.Value * statRadiatedPower.Value * statAperture.Value * 0.000625f / (Math.PI * Math.PI) / Math.Pow(10f, statSensitivity.Value / 10f);
                double rangeToSensitivityLimit = Math.Pow(rangeToSensitivityLimit4, (double)1 / 4) * 10f;

                if (rangeToPowerLimit < 0 || rangeToSensitivityLimit < 0)
                {
                    Debug.LogError(rangeToPowerLimit + " vs " + rangeToSensitivityLimit);
                    break;
                }

                double range = Math.Min(rangeToPowerLimit, rangeToSensitivityLimit);

                if (range >= statMaxRange.Value * 10f)
                {
                    __result = __result + "Detection of " + signature + " m^2 Signature: " + string.Format("{0:0.## km}", statMaxRange.Value / 100f);
                }
                else
                {
                    __result = __result + "Detection of " + signature + " m^2 Signature: " + string.Format("{0:0.## km}", range / 1000f);
                }
            }

            __result = __result + "\n";
        }
    }

    [HarmonyPatch(typeof(HullComponent), "GetFormattedStats", typeof(bool), typeof(int))]
    class Patch_HullComponent_GetFormattedStats
    {
        static void Postfix(ref HullComponent __instance, ref bool full, ref string __result)
        {
            if (!full) return;

            StatValue statMaxHP = (StatValue)Utilities.GetPrivateField(__instance, "_statMaxHP");
            StatValue statDamageThreshold = (StatValue)Utilities.GetPrivateField(__instance, "_statDamageThreshold");
            StatValue statRareDebuffChance = (StatValue)Utilities.GetPrivateField(__instance, "_statRareDebuffChance");
            List<ComponentDebuff> rareDebuffTable = (List<ComponentDebuff>)Utilities.GetPrivateField(__instance, "_rareDebuffTable");
            bool reinforced = (bool)Utilities.GetPrivateField(__instance, "_reinforced");
            float mass = (float)Utilities.GetPrivateField(__instance, "_mass");
            float functioningThreshold = (float)Utilities.GetPrivateField(__instance, "_functioningThreshold");
            Ships.Priority dcPriority = (Ships.Priority)Utilities.GetPrivateField(__instance, "_dcPriority");

            __result = string.Format("{0}\nHitpoints to Function: {1}\n{2}{3}\n{6}\nMass: {4} Tonnes\n{5}", new object[]
            {
                statMaxHP.FullTextWithLink,
                (int)functioningThreshold,
                statDamageThreshold.FullTextWithLink,
                reinforced ? " (Reinforced)" : "",
                mass,
                (rareDebuffTable.Count > 0) ? (statRareDebuffChance.FullTextWithLink + "\n") : "",
                "DC Priority: " + dcPriority.ToString()
            });
        }
    }
    /*
    [HarmonyPatch(typeof(FriendlyShipItem), "HandleStructureBroken")]
    class Patch_FriendlyShipItem_HandleStructureBroken
    {
        static bool Prefix(ref FriendlyShipItem __instance)
        {
            ShipController ship = (ShipController)Utilities.GetPrivateField(__instance, "_ship");
            if (ship != null)
            {
                HullStructure structure = ship.Ship.Hull._structure;

                if (structure != null)
                {
                    //Debug.Log("SHS STRUCTURE: hull valid");
                    StatusIcon structureIcon = (StatusIcon)Utilities.GetPrivateField(__instance, "_structureIcon");

                    if (structureIcon != null)
                    {
                        //Debug.Log("SHS STRUCTURE: icon valid");
                        structureIcon.Show();
                        structureIcon.ChangedFlash();

                        float maxHealth = (float)Utilities.GetPrivateField(structure, "_maxHealth");
                        Graphic graphic = (Graphic)Utilities.GetPrivateField(structureIcon, "_graphic");

                        //Debug.Log("SHS STRUCTURE: " + structure.CurrentHealth + "/" + maxHealth);
                        if (structure.IsDestroyed)
                        {
                            structureIcon.UpdateTooltipText("Structure Broken: damage dealt to empty areas of the ship will be transferred to intact components");
                            
                            graphic.color = GameColors.Red;
                        }
                        else
                        {
                            structureIcon.UpdateTooltipText("Structural Integrity: " + string.Format("{0:0}%", 100 * structure.CurrentHealth / maxHealth) + " " + string.Format("({0:0}/{1:0})", structure.CurrentHealth, maxHealth));

                            if (structure.CurrentHealth / maxHealth < 0.5f)
                            {
                                graphic.color = GameColors.Yellow;
                            }
                            else
                            {
                                graphic.color = GameColors.Green;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
    
    [HarmonyPatch(typeof(HullStructure), "Game.ISubDamageable.DoDamage")]
    class Patch_HullStructure_DoDamage
    {
        static void Postfix(ref HullStructure __instance, ref bool __result)
        {
            if (__result == true)
            {
                return;
            }

            if (SkirmishGameManager.Instance.IsSoloGame)
            {
                FriendlyShipList shipList = SkirmishGameManager.Instance.UI.MyShipList;
                List<FriendlyShipItem> shipItems = (List<FriendlyShipItem>)Utilities.GetPrivateField(shipList, "_ships");
                Ship ship = (Ship)Utilities.GetPrivateField(__instance, "_ship");

                foreach (FriendlyShipItem shipItem in shipItems)
                {
                    if (shipItem.Ship.Ship == ship)
                    {
                        object[] parameters = { __result };
                        Utilities.CallPrivateMethod(shipItem, "HandleStructureBroken", parameters);
                        return;
                    }
                }
            }
        }
    }
    */
}



/*

E = ((P / (12.566371f * R * R) * G) * SIG) / (12.566371f * R * R) * G;
E / G = ((P / (12.566371f * R * R) * G) * SIG) / (12.566371f * R * R);
E / G * (12.566371f * R * R) = ((P / (12.566371f * R * R) * G) * SIG);
E / G * (12.566371f * R * R) / (P / (12.566371f * R * R) * G) = SIG;
E / G * (12.566371f * R * R) / P * (12.566371f * R * R) / G = SIG;
E * (12.566371f * R * R) * (12.566371f * R * R) = SIG * G * G * P;
12.566371f * R * R * 12.566371f * R * R = SIG * G * G * P / E;
R * R * R * R = SIG * G * G * P / E / 12.566371f / 12.566371f;
R = root4(SIG * G^2 * P / E / 12.566371f^2)

F = E * A
E = F / A
E = F / A

F = N

R = root4(SIG * G^2 * P / (N / A) / 12.566371f^2)
R = root4(SIG * G^2 * P * A / N / 12.566371f^2)

R = root4(SIG * G^2 * P * A / N / 12.566371f^2)
8550 = root4(600 * 40 * 40 * 3500 * 20 / 1 / 157.91367)
8550 = root4(336000000 * 20 / 157.91367)
8550 = root4(8400000000 / 157.91367)
8550 = root4(53193621.55)
8550 = 85.4013803

S = LOG10(F * 0.0001) * 10
10^(S / 10) = F * 0.0001
F = 10^(S / 10) / 0.0001
E * A = 10^(S / 10) / 0.0001
E = 10^(S / 10) / A / 0.0001
((P / (12.566371f * R * R) * G) * SIG) / (12.566371f * R * R) * G = 10^(S / 10) / A / 0.0001
10^(S / 10) / A / 0.0001 = P / 12.566371f / R / R * G * SIG / 12.566371f / R / R * G
10^(S / 10) / A / 0.0001 = P * G * G * SIG / 12.566371f / 12.566371f / R / R / R / R
10^(S / 10) / A / 0.0001 * R^4 = P * G * G * SIG / 12.566371f / 12.566371f
10^(S / 10) * R^4 = P * G * G * SIG * A * 0.0001 / 12.566371f / 12.566371f
R^4 = P * G * G * SIG * A * 0.0001 / 12.566371f / 12.566371f / 10^(S / 10)
R^4 = P * G^2 * SIG * A * 0.0001 / 12.566371f^2 / 10^(S / 10)
R = root4(P * G^2 * SIG * A * 0.0001 / 12.566371f^2 / 10^(S / 10))
R = root4(P * G^2 * SIG * A * 0.0001 / (4 * PI)^2 / 10^(S / 10))
R = root4(P * G^2 * SIG * A * 0.0001 / 16 / PI^2 / 10^(S / 10))
R = root4(P * G^2 * SIG * A * 0.00000625 / PI^2 / 10^(S / 10))

Blurb:
R = root4(P * G^2 * SIG * A * 6.25 / PI^2 * 10^(-S / 10))


R = (SIG * G^2 * P * A / N / 12.566371f^2)^0.25

N = (AMBIENT + (J * G)) * 10^(-0.07 * ARR)
N = (1 + (J * G)) * 10^(-0.07 * ARR)

J = SUMMATION(i = 0 to N-1){AJP * e^(-(i / 2.75)^2)}
J = AJP * SUMMATION(i = 0 to N-1){e^(-(i / 2.75)^2)}

AJP = (blankets(blanket_power * blanket_gain / 4 / PI / Range^2) + j15s(j15_power * j15_gain / 4 / PI / Range^2)) / (blankets + j15s)
AJP = (blankets(blanket_power * blanket_gain / 4 / PI) + j15s(j15_power * j15_gain / 4 / PI)) / (blankets + j15s) / Range^2
AJP = (blankets(blanket_power * blanket_gain) + j15s(j15_power * j15_gain)) / (blankets + j15s) / 4 / PI / Range^2

J = (blankets(blanket_power * blanket_gain) + j15s(j15_power * j15_gain)) / (blankets + j15s) / 4 / PI / Range^2 * SUMMATION(i = 0 to N-1){e^(-(i / 2.75)^2)}

R = [SIG * G^2 * P * A / (4*PI)^2 / ((1 + (J*R / R * G)) * 10^(-0.07 * ARR))]^0.25
R^4 = SIG * G^2 * P * A / (4*PI)^2 / ((1 + (J*R / R * G)) * 10^(-0.07 * ARR))
R^4 * (1 + (J*R / R * G)) = SIG * G^2 * P * A / (4*PI)^2 / 10^(-0.07 * ARR)
R^4 + (R^3 * J*R * G) = SIG * G^2 * P * A / (4*PI)^2 / 10^(-0.07 * ARR)

 * */