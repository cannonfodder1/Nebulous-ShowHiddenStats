using Bundles;
using Game;
using Game.EWar;
using Game.Sensors;
using Game.UI;
using Game.Units;
using HarmonyLib;
using Modding;
using Munitions;
using Ships;
using Ships.Controls;
using SmallCraft;
using SmallCraft.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utility;
using static SmallCraft.SerializedCraftLoadout;

namespace ShowHiddenStats
{
    public class ShowHiddenStats : IModEntryPoint
    {
        public static Ship CurrentlyEditingShip;

        public void PostLoad()
        {
            Harmony harmony = new Harmony("nebulous.show-hidden-stats");
            harmony.PatchAll();
        }

        public void PreLoad()
        {

        }

		public static void ReplaceStatLabel(List<(string, string)> StatList, string TargetLabel, string ReplaceWith, string NewValue = "")
		{
			ValueTuple<string, string> replaceItem = StatList.Find(tuple => tuple.Item1 == TargetLabel);
			int replaceIndex = StatList.FindIndex(tuple => tuple.Item1 == TargetLabel);
			replaceItem.Item1 = ReplaceWith;

            if (NewValue != "")
            {
                replaceItem.Item2 = NewValue;
            }

			StatList.Insert(replaceIndex, replaceItem);
			StatList.RemoveAt(replaceIndex + 1);
		}

		public static ValueTuple<string, string> GetArmorShreddingStat(float armorDamageRadius)
        {
            if (armorDamageRadius > 0)
            {
                return new ValueTuple<string, string>("Armor Shredding Radius", armorDamageRadius + " m");
            }
            else
			{
				return new ValueTuple<string, string>("Armor Shredding Radius", "NONE");
			}
        }

        public static ValueTuple<string, string> GetOverpenDamageStat(float overpenDamageMultiplier)
        {
            if (overpenDamageMultiplier < 1)
			{
				return new ValueTuple<string, string>("Overpenetration Damage", overpenDamageMultiplier + " m");
			}
			else
			{
				return new ValueTuple<string, string>("Overpenetration Damage", "FULL");
			}
		}

        public static ValueTuple<string, string> GetCriticalEventStat(float randomEffectMultiplier)
        {
            if (randomEffectMultiplier != 1f)
            {
                randomEffectMultiplier = (randomEffectMultiplier - 1) * 100;
                string prefix = (randomEffectMultiplier < 0) ? "" : "+";
				return new ValueTuple<string, string>("Crit Chance Modifier", prefix + randomEffectMultiplier + " %");
			}
            else
			{
				return new ValueTuple<string, string>("Crit Chance Modifier", "BASE");
			}
        }

        public static ValueTuple<string, string> GetCrewDamageStat(float crewVulnerabilityMultiplier)
        {
            if (crewVulnerabilityMultiplier != 1f)
            {
                crewVulnerabilityMultiplier = (crewVulnerabilityMultiplier - 1) * 100;
                string prefix = (crewVulnerabilityMultiplier < 0) ? "" : "+";
				return new ValueTuple<string, string>("Crew Damage Modifier", prefix + crewVulnerabilityMultiplier + " %");
			}
            else
            {
				return new ValueTuple<string, string>("Crew Damage Modifier", "BASE");
			}
        }

        public static List<ValueTuple<string, string>> GetDamageFalloffStat(AnimationCurve damageFalloff, float maxRange)
        {
            if (damageFalloff == null)
            {
                throw new ArgumentNullException(nameof(damageFalloff));
            }

			List<ValueTuple<string, string>> result = new List<ValueTuple<string, string>>();
			result.Add(new ValueTuple<string, string>("Damage and Penetration at Range", ""));

			float[] distances = { maxRange * (1.0f / 4.0f), maxRange * (2.0f / 4.0f), maxRange * (3.0f / 4.0f), maxRange };

			foreach (float distance in distances)
            {
                float damage = damageFalloff.Evaluate(distance / maxRange) * 100;
				result.Add(new ValueTuple<string, string>(string.Format(" - {0:0.00} km", distance * 10f / 1000f), string.Format("{0:N0}%", damage)));
			}

            return result;
        }

        public static float[] FindThrusterStrengthValues(BaseHull hull)
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

		public static double CalculateDetectionRange(float signature, float radiatedPower, float gain, float apertureSize, float sensitivity)
        {
			// = 100 * (power * gain^2 * aperture * signature / (4*PI())^2 / noise / 100)^0.25
			double rangeToPowerLimit4 = signature * gain * gain * radiatedPower * apertureSize / (16 * Math.PI * Math.PI) / 100f;
			double rangeToPowerLimit = Math.Pow(rangeToPowerLimit4, (double)1 / 4) * 100f;

			// = 10 * (power * gain^2 * aperture * signature * 0.000625 / PI()^2 / 10^(sensitivity / 10))^0.25
			double rangeToSensitivityLimit4 = signature * gain * gain * radiatedPower * apertureSize * 0.000625f / (Math.PI * Math.PI) / Math.Pow(10f, sensitivity / 10f);
			double rangeToSensitivityLimit = Math.Pow(rangeToSensitivityLimit4, (double)1 / 4) * 10f;

			if (rangeToPowerLimit < 0 || rangeToSensitivityLimit < 0)
			{
				Debug.LogError("(SHS) error in search radar power/sensitivity calculation, final numbers should not be negative: " + rangeToPowerLimit + " and " + rangeToSensitivityLimit);
				return 0;
			}

			return Math.Min(rangeToPowerLimit, rangeToSensitivityLimit);
		}

        public static int GetBaselinePowerRequirement(ref List<HullComponent> consumerList)
        {
            int total = 0;
            List<HullComponent> removalList = new List<HullComponent>();

            //Debug.Log("BASELINE POWER DRAW");

            foreach (HullComponent component in consumerList)
            {
                if (component == null)
                {
                    continue;
                }

                if (IsBaselineComponent(component))
                {
                    foreach (ResourceModifier resource in component.ResourcesRequired)
                    {
                        if (resource.ResourceName == "Power")
                        {
                            //Debug.Log(" - " + component.ComponentName);

                            total += resource.Amount;
                            removalList.Add(component);
                        }
                    }
                }
            }

            foreach (HullComponent component in removalList)
            {
                consumerList.Remove(component);
            }

			return total;
		}

		public static int GetDefensivePowerRequirement(ref List<HullComponent> consumerList)
		{
			int total = 0;
			List<HullComponent> removalList = new List<HullComponent>();

			//Debug.Log("DEFENSIVE POWER DRAW");

			foreach (HullComponent component in consumerList)
			{
				if (component == null)
				{
					continue;
				}

				if (IsDefensiveComponent(component))
				{
					foreach (ResourceModifier resource in component.ResourcesRequired)
					{
						if (resource.ResourceName == "Power")
						{
							//Debug.Log(" - " + component.ComponentName);

							total += resource.Amount;
							removalList.Add(component);
						}
					}
				}
			}

			foreach (HullComponent component in removalList)
			{
				consumerList.Remove(component);
			}

			return total;
		}

		public static int GetOffensivePowerRequirement(ref List<HullComponent> consumerList)
		{
			int total = 0;
			List<HullComponent> removalList = new List<HullComponent>();

			//Debug.Log("OFFENSIVE POWER DRAW");

			foreach (HullComponent component in consumerList)
			{
                if (component == null)
                {
                    continue;
                }

				if (IsOffensiveComponent(component))
				{
					foreach (ResourceModifier resource in component.ResourcesRequired)
					{
						if (resource.ResourceName == "Power")
						{
							//Debug.Log(" - " + component.ComponentName);

							total += resource.Amount;
							removalList.Add(component);
						}
					}
				}
			}

			foreach (HullComponent component in removalList)
			{
				consumerList.Remove(component);
			}

			return total;
		}

		public static bool IsBaselineComponent(HullComponent component)
		{
			if (component == null)
			{
				throw new ArgumentNullException(nameof(component));
			}

			if (component is BaseCellLauncherComponent)
			{
				return true;
			}

            if (component is WeaponComponent)
            {
                return false;
			}

			if (component is FixedActiveSensorComponent || component is TurretedActiveSensorComponent || component is SensorTurretComponent)
			{
				return false;
			}

			return true;
		}

		public static bool IsDefensiveComponent(HullComponent component)
		{
			if (component == null)
			{
				throw new ArgumentNullException(nameof(component));
			}

			if (component is BaseCellLauncherComponent)
			{
				return false;
			}

			if (component is WeaponComponent)
			{
				WeaponComponent weapon = (WeaponComponent)component;

				if (weapon.EWType == EWarWeaponType.Jammer)
				{
					return true;
				}

				WeaponRole role = (WeaponRole)Utilities.GetPrivateField(weapon, "_role");
				if (role == WeaponRole.Defensive || role == WeaponRole.Utility)
				{
					return true;
				}

				if (!weapon.SupportsPositionTargeting && !weapon.SupportsTrackTargeting && !weapon.SupportsVisualTargeting)
				{
					return true;
				}
			}

			return false;
		}

		public static bool IsOffensiveComponent(HullComponent component)
		{
			if (component == null)
			{
				throw new ArgumentNullException(nameof(component));
			}

			if (component is BaseCellLauncherComponent)
			{
				return false;
			}

			if (component is FixedActiveSensorComponent || component is TurretedActiveSensorComponent || component is SensorTurretComponent)
			{
				return true;
			}

			if (component is WeaponComponent)
			{
				WeaponComponent weapon = (WeaponComponent)component;

                if (weapon.EWType == EWarWeaponType.Jammer)
				{
					return false;
                }

				if (weapon.EWType == EWarWeaponType.Sensor || weapon.EWType == EWarWeaponType.Illuminator)
				{
					return true;
				}

                WeaponRole role = (WeaponRole)Utilities.GetPrivateField(weapon, "_role");
                if (role == WeaponRole.Offensive || role == WeaponRole.None)
				{
					return true;
				}

				if (weapon.SupportsPositionTargeting || weapon.SupportsTrackTargeting || weapon.SupportsVisualTargeting)
				{
					return true;
                }
            }

			return false;
		}
	}
    
    [HarmonyPatch(typeof(BaseHull), "EditorFormatHullStats")]
    class Patch_BaseHull_EditorFormatHullStats
    {
        static void Postfix(ref BaseHull __instance, ref List<ValueTuple<string, string>> hull, ref List<ValueTuple<string, string>> sigs, ref bool showBreakdown)
		{
			try
			{
				StatValue statInternalDensity = (StatValue)Utilities.GetPrivateField(__instance, "_statInternalDensity");
				StatValue statMaxRepair = (StatValue)Utilities.GetPrivateField(__instance, "_statMaxRepair");

				string internalDensity = "\n" + "Internal Density: " + statInternalDensity.Value + " " + statInternalDensity.Unit;

				int insertionPoint = hull.FindIndex(tuple => tuple.Item1 == "$SHIPSTAT_COMPONENTDR");
				hull.Insert(insertionPoint, new ValueTuple<string, string>("Internal Density", statInternalDensity.Value + " " + statInternalDensity.Unit));

				string fullMaxRepStr = string.Format("{0:0} %", statMaxRepair.Value * 100f);
				if (showBreakdown == false) // We are in the Add Ship menu
				{
					float valueMaxRep = statMaxRepair.Value;
					foreach (StatModifier modifier in __instance.BaseModifiers)
					{
						if (modifier.StatName == statMaxRepair.StatID.FullType)
						{
							valueMaxRep += modifier.Literal;
						}
					}

					fullMaxRepStr = string.Format("{0:0} %", valueMaxRep * 100f);
				}
				else // We are in the fleet editor ship summary panel
				{
					float bonusMaxRep = (statMaxRepair.Value - statMaxRepair.BaseValue);
					if (bonusMaxRep != 0f) fullMaxRepStr = fullMaxRepStr + " (" + StatModifier.FormatModifierColored(bonusMaxRep, false) + ")";
				}

				insertionPoint = hull.FindIndex(tuple => tuple.Item1 == "$SHIPSTAT_CREWCOMP_BASE");
				hull.Insert(insertionPoint, new ValueTuple<string, string>("Max Repair", fullMaxRepStr));

				StatValue statIdentityWorkRequired = (StatValue)Utilities.GetPrivateField(__instance, "_statIdentityWorkRequired");

				string intelSummary = "";
				intelSummary = intelSummary + "Time Unidentified vs Basic CIC: " + string.Format("{0:0} s", statIdentityWorkRequired.Value / 1) + "\n";
				intelSummary = intelSummary + "Time Unidentified vs Citadel CIC: " + string.Format("{0:0} s", statIdentityWorkRequired.Value / 4) + "\n";
				intelSummary = intelSummary + "Time Unidentified vs Intel Centre: " + string.Format("{0:0} s", statIdentityWorkRequired.Value / 15) + "\n";

				hull.Add(new ValueTuple<string, string>("Time Unidentified vs Basic CIC", string.Format("{0:0} s", statIdentityWorkRequired.Value / 1)));
				hull.Add(new ValueTuple<string, string>("Time Unidentified vs Citadel CIC", string.Format("{0:0} s", statIdentityWorkRequired.Value / 4)));
				hull.Add(new ValueTuple<string, string>("Time Unidentified vs Intel Centre", string.Format("{0:0} s", statIdentityWorkRequired.Value / 15)));



				StatValue sigMultRadar = (StatValue)Utilities.GetPrivateField(__instance, "_statSigMultRadar");
				List<HullComponent> searchRadars = BundleManager.Instance.AllComponents.ToList().FindAll(x => x is BaseActiveSensorComponent);

				insertionPoint = sigs.FindIndex(tuple => tuple.Item1 == "$SHIPSTAT_WAKESIG");
				if (searchRadars.Count > 0)
				{
					sigs.Insert(insertionPoint, new ValueTuple<string, string>("Detected At Range", ""));
					insertionPoint++;
				}

				foreach (HullComponent component in searchRadars)
				{
					BaseActiveSensorComponent searchRadar = (BaseActiveSensorComponent)component;

					if (searchRadar != null)
					{
						float statMaxRange = (float)Utilities.GetPrivateField(searchRadar, "_maxRange");
						float radiatedPower = (float)Utilities.GetPrivateField(searchRadar, "_radiatedPower");
						float apertureSize = (float)Utilities.GetPrivateField(searchRadar, "_apertureSize");
						float gain = (float)Utilities.GetPrivateField(searchRadar, "_gain");
						float sensitivity = (float)Utilities.GetPrivateField(searchRadar, "_sensitivity");

						double range = ShowHiddenStats.CalculateDetectionRange(sigMultRadar.Value * 10f, radiatedPower, gain, apertureSize, sensitivity);
						string rowHeader = " - " + searchRadar.ComponentName;

						if (range >= statMaxRange * 10f)
						{
							sigs.Insert(insertionPoint, new ValueTuple<string, string>(rowHeader, string.Format("{0:0.## km}", statMaxRange / 100f)));
						}
						else
						{
							sigs.Insert(insertionPoint, new ValueTuple<string, string>(rowHeader, string.Format("{0:0.## km}", range / 1000f)));
						}
					}
				}



				StatValue statSigPowerWake = (StatValue)Utilities.GetPrivateField(__instance, "_statSigPowerWake");
				string notValidated = "<color=" + GameColors.GreenTextColor + ">SAFE</color>";
				string yesValidated = "<color=" + GameColors.RedTextColor + ">VALIDATED</color>";

				sigs.Add(new ValueTuple<string, string>("Fore-Aspect Validated by THERM", statSigPowerWake.Value >= 175.0f ? yesValidated : notValidated));

				if (statSigPowerWake.Value >= 175.0f)
				{
					float percentToDecay = 1f - (175.0f / statSigPowerWake.Value);

					sigs.Add(new ValueTuple<string, string>("Validated After Engine Shutoff", string.Format("{0:0.## s}", percentToDecay * 30.0f)));
				}
			}
			catch (Exception e)
			{
				Debug.LogError("Show Hidden Stats has encountered a fatal error in the EditorFormatHullStats() override and is aborting to allow the fleet file to be loaded. Please relay this to the mod author. Here follows the root error:\n" + e.Message);
			}
		}
    }
    
    [HarmonyPatch(typeof(BaseHull), "EditorFormatPropulsionStats")]
    class Patch_BaseHull_EditorFormatPropulsionStats
    {
		static void Postfix(ref BaseHull __instance, ref List<ValueTuple<string, string>> __result)
		{
			try
			{
				List<DriveComponent> propulsionComponents = __instance.CollectComponents<DriveComponent>();
				if (propulsionComponents != null && propulsionComponents.Count > 0)
				{
					float[] thrusterStrengthValues = ShowHiddenStats.FindThrusterStrengthValues(__instance);

					int insertionPoint = __result.FindIndex(tuple => tuple.Item1 == "$SHIPSTAT_ACCELTIME");
					__result.Insert(insertionPoint, new ValueTuple<string, string>(" - Main Thrusters", string.Format("{0:0.##}%", thrusterStrengthValues[0] * 100)));
					__result.Insert(insertionPoint, new ValueTuple<string, string>(" - Fore Thrusters", string.Format("{0:0.##}%", thrusterStrengthValues[1] * 100)));
					__result.Insert(insertionPoint, new ValueTuple<string, string>(" - Side Thrusters", string.Format("{0:0.##}%", thrusterStrengthValues[2] * 100)));

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

					ShowHiddenStats.ReplaceStatLabel(__result, "$SHIPSTAT_TURNRATE", "Theoretical Turn Rate");

					string actualTurnStr = string.Format("{0:0.##} deg/s", finalTrueMaxTurnRate);
					if (modifierTrueMaxTurnRate != 0.0f) actualTurnStr = actualTurnStr + " (" + StatModifier.FormatModifierColored(modifierTrueMaxTurnRate, false) + ")";
					__result.Add(new ValueTuple<string, string>("Actual Max Turn Rate", actualTurnStr));

					string estimatedTurnStr = string.Format("{0:0} s", finalTimeToTurn180);
					if (modifierTimeToTurn180 != 0.0f) estimatedTurnStr = estimatedTurnStr + " (" + StatModifier.FormatModifierColored(modifierTimeToTurn180, true) + ")";
					__result.Add(new ValueTuple<string, string>("Estimated 180 Turn", estimatedTurnStr));

					string estimatedRollStr = string.Format("{0:0} s", finalTimeToRoll180);
					if (modifierTimeToRoll180 != 0.0f) estimatedRollStr = estimatedRollStr + " (" + StatModifier.FormatModifierColored(modifierTimeToRoll180, true) + ")";
					__result.Add(new ValueTuple<string, string>("Estimated 180 Roll", estimatedRollStr));
				}
			}
			catch (Exception e)
			{
				Debug.LogError("Show Hidden Stats has encountered a fatal error in the EditorFormatPropulsionStats() override and is aborting to allow the fleet file to be loaded. Please relay this to the mod author. Here follows the root error:\n" + e.Message);
			}
		}
	}

	[HarmonyPatch(typeof(ResourcePool), "CalculateDemandForEditor")]
	class Patch_ResourcePool_CalculateDemandForEditor
	{
		static void Postfix(ref ResourcePool __instance)
		{
			if (__instance.Resource.Name == "Power")
			{
				string editorDetails = (string)Utilities.GetPrivateField(__instance, "_editorDetails");
				List<HullComponent> consumers = (List<HullComponent>)Utilities.GetPrivateField(__instance, "_consumers");

				TextMeshProUGUI detailText = (TextMeshProUGUI)Utilities.GetPrivateField(__instance, "_detailText");

				//Debug.Log("ALL POWER CONSUMERS");
				//foreach (HullComponent component in consumers)
				//{
				//	Debug.Log(" - " + component.ComponentName);
				//}
				
                int basepower = ShowHiddenStats.GetBaselinePowerRequirement(ref consumers);
				int defpower = ShowHiddenStats.GetDefensivePowerRequirement(ref consumers);
				int offpower = ShowHiddenStats.GetOffensivePowerRequirement(ref consumers);

                string baseprefix = "Constant Power Draw: ";
                string defprefix = "Defensive Power Draw: ";
                string offprefix = "Offensive Power Draw: ";

				string baseline = baseprefix + basepower + " kW";
                if (basepower > __instance.TotalAvailable)
                {
                    baseline = baseprefix + "<color=red>" + basepower + " kW" + "</color>";
				}

				string defensive = defprefix + (basepower + defpower) + " kW";
				if ((basepower + defpower) > __instance.TotalAvailable)
				{
					defensive = defprefix + "<color=red>" + (basepower + defpower) + " kW" + "</color>";
				}

				string offensive = offprefix + (basepower + offpower) + " kW";
				if ((basepower + offpower) > __instance.TotalAvailable)
				{
					offensive = offprefix + "<color=red>" + (basepower + offpower) + " kW" + "</color>";
				}

				editorDetails = baseline + "\n" + offensive + "\n" + defensive + "\n" + "\n" + editorDetails;
				Utilities.SetPrivateField(__instance, "_editorDetails", editorDetails);
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

			if (armorDamageRadius > 0)
			{
				__result = __result + "\n" + "Armor Shredding Radius: " + armorDamageRadius + " m";
			}
			if (overpenDamageMultiplier < 1)
			{
				__result = __result + "\n" + "Overpenetration Damage: " + (overpenDamageMultiplier * 100) + " %";
			}
			if (randomEffectMultiplier != 1f)
			{
				randomEffectMultiplier = (randomEffectMultiplier - 1) * 100;
				string prefix = (randomEffectMultiplier < 0) ? "" : "+";
				__result = __result + "\n" + "Crit Chance Modifier: " + prefix + randomEffectMultiplier + " %";
			}
			if (crewVulnerabilityMultiplier != 1f)
			{
				crewVulnerabilityMultiplier = (crewVulnerabilityMultiplier - 1) * 100;
				string prefix = (crewVulnerabilityMultiplier < 0) ? "" : "+";
				__result = __result + "\n" + "Crew Damage Modifier: " + prefix + crewVulnerabilityMultiplier + " %";
			}

			if (__instance is LightweightSplashingShell)
            {
				LightweightSplashingShell splashingShell = (LightweightSplashingShell)__instance;

				bool componentDamageFallsOff = (bool)Utilities.GetPrivateField(splashingShell, "_componentDamageFallsOff");
				AnimationCurve damageFalloff = (AnimationCurve)Utilities.GetPrivateField(splashingShell, "_damageFalloff");
				float maxFlightTime = (float)Utilities.GetPrivateField(splashingShell, "_maxFlightTime");
				float flightSpeed = (float)Utilities.GetPrivateField(splashingShell, "_flightSpeed");

				if (componentDamageFallsOff && damageFalloff != null)
                {
                    float maxRange = maxFlightTime * flightSpeed;

					__result = __result + "\n";
					__result = __result + "Damage and Penetration at Range:";

					float[] distances = { maxRange * (1.0f / 4.0f), maxRange * (2.0f / 4.0f), maxRange * (3.0f / 4.0f), maxRange };

					foreach (float distance in distances)
					{
						__result = __result + "\n";
						float damage = damageFalloff.Evaluate(distance / maxRange) * 100;
						__result = __result + string.Format(" - {0:N0}% at {1:0.00} km", damage, distance * 10f / 1000f);
					}
				}
			}

			if (__instance is LightweightAirburstFragShell)
			{
				LightweightAirburstFragShell fragmentationShell = (LightweightAirburstFragShell)__instance;

				float blastRadius = (float)Utilities.GetPrivateField(fragmentationShell, "_blastRadius");

				__result = __result + "\n";
				__result = __result + "Blast Radius: " + (blastRadius * 10f) + " m";
			}

			if (__instance is LightweightProximityShell)
			{
				LightweightProximityShell proximityShell = (LightweightProximityShell)__instance;

				float triggerRadius = (float)Utilities.GetPrivateField(proximityShell, "_triggerRadius");

				__result = __result + "\n";
				__result = __result + "Fuse Trigger Radius: " + (triggerRadius * 10f) + " m";
			}

			if (__instance is LightweightClusterShell)
			{
				LightweightClusterShell clusterShell = (LightweightClusterShell)__instance;

				float lookaheadSphereRadius = (float)Utilities.GetPrivateField(clusterShell, "_lookaheadSphereRadius");

				__result = __result + "\n";
				__result = __result + "Pellet Spread Radius: " + (lookaheadSphereRadius * 10f) + " m";
			}

			if (__instance is NonphysicalMunition)
			{
				NonphysicalMunition nonPhysicalMunition = (NonphysicalMunition)__instance;

				bool useSphereCast = (bool)Utilities.GetPrivateField(nonPhysicalMunition, "_useSphereCast");
				float sphereCastRadius = (float)Utilities.GetPrivateField(nonPhysicalMunition, "_sphereCastRadius");

				if (useSphereCast)
				{
					__result = __result + "\n";
					__result = __result + "Pellet Spread Radius: " + (sphereCastRadius * 10f) + " m";
				}
			}
		}
	}

	[HarmonyPatch(typeof(LightweightMunitionBase), "GetDamageStatsText")]
	class Patch_LightweightMunitionBase_GetDamageStatsText
	{
		static void Postfix(ref LightweightMunitionBase __instance, ref string __result)
		{
			__result = __result.Replace("Penetration Depth", "Maximum Penetration Depth");

			float overrideComponentSearchDistance = (float)Utilities.GetPrivateField(__instance, "_overrideComponentSearchDistance");
			if (overrideComponentSearchDistance > 0)
			{
				if (__result.Contains("Maximum Penetration Depth"))
				{
					__result = __result.Remove(__result.IndexOf("Maximum Penetration Depth"));
				}

				__result = __result + "Guaranteed Penetration Depth: " + (overrideComponentSearchDistance * 10) + " m" + "\n";
			}

			if (__instance is LightweightKineticShell)
			{
				LightweightKineticShell kineticShell = (LightweightKineticShell)__instance;

				CastType castType = (CastType)Utilities.GetPrivateField(kineticShell, "_castType");

                if (castType == CastType.RayCone)
                {
                    float componentDamage = (float)Utilities.GetPrivateField(kineticShell, "_componentDamage");
                    float rayAngle = (float)Utilities.GetPrivateField(kineticShell, "_rayAngle");
                    int rayCount = (int)Utilities.GetPrivateField(kineticShell, "_rayCount");

                    __result = __result + "Damage Cone Angle: " + rayAngle + " degrees" + "\n";
                    __result = __result + "Damage Ray Count: " + rayCount + "\n";
                    __result = __result + "Damage Per Ray: " + (componentDamage / rayCount) + "\n";
                }
			}
		}
	}

	[HarmonyPatch(typeof(ContinuousWeaponComponent), "GetFormattedStats")]
    class Patch_ContinuousWeaponComponent_GetFormattedStats
    {
        static void Postfix(ref ContinuousWeaponComponent __instance, ref List<ValueTuple<string, string>> rows)
        {
            Muzzle[] muzzles = (Muzzle[])Utilities.GetPrivateField(__instance, "_muzzles");

            if (muzzles.Length < 1 || muzzles[0].GetType() != typeof(ContinuousRaycastMuzzle)) return;

            float armorDamageEffectSize = (float)Utilities.GetPrivateField(muzzles[0], "_armorDamageEffectSize");
            float randomEffectMultiplier = (float)Utilities.GetPrivateField(muzzles[0], "_randomEffectMultiplier");
            float crewVulnerabilityMultiplier = (float)Utilities.GetPrivateField(muzzles[0], "_crewVulnerabilityMultiplier");
            AnimationCurve damageFalloff = (AnimationCurve)Utilities.GetPrivateField(muzzles[0], "_powerFalloff");
            float maxRange = (float)Utilities.GetPrivateField(muzzles[0], "_raycastRange");

			rows.Add(ShowHiddenStats.GetArmorShreddingStat(armorDamageEffectSize));
			if (randomEffectMultiplier != 1f) rows.Add(ShowHiddenStats.GetCriticalEventStat(randomEffectMultiplier));
			if (crewVulnerabilityMultiplier != 1f) rows.Add(ShowHiddenStats.GetCrewDamageStat(crewVulnerabilityMultiplier));
			if (damageFalloff != null) rows.AddRange(ShowHiddenStats.GetDamageFalloffStat(damageFalloff, maxRange));
        }
    }

    [HarmonyPatch(typeof(BaseActiveSensorComponent), "GetFormattedStats")]
    class Patch_BaseActiveSensorComponent_GetFormattedStats
	{
		static int RowsBeforeCurrent;

		static bool Prefix(ref BaseActiveSensorComponent __instance, ref List<ValueTuple<string, string>> rows)
		{
			RowsBeforeCurrent = rows.Count;
			return true;
		}
		
		static void Postfix(ref BaseActiveSensorComponent __instance, ref List<ValueTuple<string, string>> rows)
		{
			try
			{
				StatValue statMaxRange = (StatValue)Utilities.GetPrivateField(__instance, "_statMaxRange");
				StatValue statRadiatedPower = (StatValue)Utilities.GetPrivateField(__instance, "_statRadiatedPower");
				StatValue statAperture = (StatValue)Utilities.GetPrivateField(__instance, "_statAperture");
				StatValue statGain = (StatValue)Utilities.GetPrivateField(__instance, "_statGain");
				StatValue statSensitivity = (StatValue)Utilities.GetPrivateField(__instance, "_statSensitivity");
				StatValue statMaxError = (StatValue)Utilities.GetPrivateField(__instance, "_statMaxError");

				int insertionPoint1 = rows.FindIndex(RowsBeforeCurrent, tuple => tuple.Item1 == "$SHIPSTAT_SIGNATURETYPE");
				rows.Insert(insertionPoint1, new ValueTuple<string, string>("Detected by ELINT", string.Format("{0:0.## km}", statMaxRange.Value * 1.25f / 100f)));

				int insertionPoint2 = rows.FindIndex(RowsBeforeCurrent, tuple => tuple.Item1 == "$SHIPSTAT_SENSITIVITY");
				rows.Insert(insertionPoint2, new ValueTuple<string, string>("Overall Effective Output", string.Format("{0:0.## GW}", statRadiatedPower.Value * statAperture.Value * statGain.Value * statGain.Value / 1000000f)));

				int insertionPoint3 = rows.FindIndex(RowsBeforeCurrent, tuple => tuple.Item1 == "$SHIPSTAT_CANLOCK");
				rows.Insert(insertionPoint3, new ValueTuple<string, string>("Track Quality", "TQ" + SensorMath.CalculateTrackQuality(statMaxError.Value)));

				float[] signatures = { 1000, 2000, 3000, 5000, 7000, 9000, 12000 };

				rows.Add(new ValueTuple<string, string>("", ""));
				rows.Add(new ValueTuple<string, string>("Detection Range by Signature Size", ""));

				foreach (float signature in signatures)
				{
					double range = ShowHiddenStats.CalculateDetectionRange(signature, statRadiatedPower.Value, statGain.Value, statAperture.Value, statSensitivity.Value);
					string rowHeader = " - Detection of " + signature + " m<sup>2</sup> Signature";

					if (range >= statMaxRange.Value * 10f)
					{
						rows.Add(new ValueTuple<string, string>(rowHeader, string.Format("{0:0.## km}", statMaxRange.Value / 100f)));
					}
					else
					{
						rows.Add(new ValueTuple<string, string>(rowHeader, string.Format("{0:0.## km}", range / 1000f)));
					}
				}

				rows.Add(new ValueTuple<string, string>("", ""));
			}
			catch (Exception e)
			{
				Debug.LogError("Show Hidden Stats has encountered a fatal error in the SensorComponent::GetFormattedStats() override and is aborting to allow the fleet file to be loaded. Please relay this to the mod author. Here follows the root error:\n" + e.Message);
			}
		}
    }

	[HarmonyPatch(typeof(HullComponent), "GetFormattedStats", typeof(List<ValueTuple<string, string>>), typeof(bool), typeof(int))]
    class Patch_HullComponent_GetFormattedStats
    {
        static void Postfix(ref HullComponent __instance, ref List<ValueTuple<string, string>> rows, ref bool full)
        {
			try
			{
				if (!full) return;

				float functioningThreshold = (float)Utilities.GetPrivateField(__instance, "_functioningThreshold");
				Ships.Priority dcPriority = (Ships.Priority)Utilities.GetPrivateField(__instance, "_dcPriority");

				int insertionPoint = rows.FindIndex(tuple => tuple.Item1 == "$SHIPSTAT_DT");
				rows.Insert(insertionPoint, new ValueTuple<string, string>("Hitpoints to Function", ((int)functioningThreshold).ToString()));
				rows.Add(new ValueTuple<string, string>("DC Priority", dcPriority.ToString()));
			}
			catch (Exception e)
			{
				Debug.LogError("Show Hidden Stats has encountered a fatal error in the HullComponent::GetFormattedStats() override and is aborting to allow the fleet file to be loaded. Please relay this to the mod author. Here follows the root error:\n" + e.Message);
			}
		}
    }

	[HarmonyPatch(typeof(Spacecraft), "GetGeneralStatsBlock")]
	class Patch_Spacecraft_GetGeneralStatsBlock
	{
		static void Postfix(ref Spacecraft __instance, ref List<ValueTuple<string, string>> __result)
		{
			if (__instance.CurrentlyEditingLoadout != null)
			{
				float totalFuel;
				float work = __instance.CalculateLoadoutChangeVolume(__instance.CurrentlyEditingLoadout, true, true, out totalFuel);
				int insertionPoint = __result.FindIndex(tuple => tuple.Item1 == "$CRAFTSTAT_PREFLIGHTTIME");
				__result.Insert(insertionPoint, new ValueTuple<string, string>("Pre-Flight Work", string.Format("{0:0.##}", work)));

				SpacecraftSocket[] sockets = (SpacecraftSocket[])Utilities.GetPrivateField(__instance, "_sockets");
				if (sockets != null && __instance.CurrentlyEditingLoadout.Elements != null)
				{
					Dictionary<string, int> totalAmmo = new Dictionary<string, int>();

					foreach (SpacecraftSocket socket in sockets)
					{
						Dictionary<string, int> socketAmmo = new Dictionary<string, int>();
						GeneralLoadoutElement element = __instance.CurrentlyEditingLoadout.Elements.FirstOrDefault((GeneralLoadoutElement x) => x.SocketKey == socket.SocketKey);

						socket.CollectAmmoTotalsForLoadout(null, ref socketAmmo, element);

						foreach (KeyValuePair<string, int> ammo in socketAmmo)
						{
							if (totalAmmo.ContainsKey(ammo.Key))
							{
								totalAmmo[ammo.Key] += ammo.Value;
							}
							else
							{
								totalAmmo.Add(ammo.Key, ammo.Value);
							}
						}
					}

					SortedDictionary<string, int> sortedAmmo = new SortedDictionary<string, int>();
					float totalPointCost = 0.0f;

					foreach (KeyValuePair<string, int> ammo in totalAmmo)
					{
						IMunition munition = BundleManager.Instance.GetMunition(ammo.Key);

						if (munition != null) // covers case where old fleet files have now-removed munitions in them, like the RBU-15
						{
							totalPointCost += (float)munition.PointCost * (float)ammo.Value / (float)munition.PointDivision;
							sortedAmmo.Add(munition.MunitionName, ammo.Value);
						}
					}

					__result.Add(new ValueTuple<string, string>("", ""));
					__result.Add(new ValueTuple<string, string>("Inventory", ""));

					foreach (KeyValuePair<string, int> ammo in sortedAmmo)
					{
						__result.Add(new ValueTuple<string, string>(" - " + ammo.Key, ammo.Value.ToString()));
					}

					__result.Add(new ValueTuple<string, string>("Cost To Arm", totalPointCost.ToString("N2")));
				}
			}
		}
	}

	[HarmonyPatch(typeof(BaseCraftMovement), "GetFlightStatsBlock")]
	class Patch_BaseCraftMovement_GetFlightStatsBlock
	{
		static void Postfix(ref BaseCraftMovement __instance, ref List<ValueTuple<string, string>> __result, ref float? loadoutMass)
		{
			float[] modeThrottles = (float[])Utilities.GetPrivateField(__instance, "_modeThrottles");
			float baseMass = (float)Utilities.GetPrivateField(__instance, "_baseMass");
			float thrustToWeight = (modeThrottles[2] * __instance.MotorPower) / (baseMass + loadoutMass.GetValueOrDefault());
			__result.Add(new ValueTuple<string, string>("Thrust-to-Weight", thrustToWeight.ToString("N2")));
		}
	}

	[HarmonyPatch(typeof(Spacecraft), "GetSensorStatsBlock")]
	class Patch_Spacecraft_GetSensorStatsBlock
	{
		static void Postfix(ref Spacecraft __instance, ref List<ValueTuple<string, string>> __result)
		{
			ISignature radarSignature = (ISignature)Utilities.GetPrivateField(__instance, "_radarSignature");

			if (radarSignature != null)
			{
				__result.Insert(0, new ValueTuple<string, string>("Radar Signature (Actual)", (radarSignature.MaxSigSize * 10) + "m<sup>2</sup>"));
				__result.Insert(0, new ValueTuple<string, string>("Radar Signature (Listed)", radarSignature.MaxSigSize + "m<sup>2</sup>"));
			}
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

	/*
	[HarmonyPatch(typeof(DiscreteWeaponComponent), "GetFormattedStats")]
	class Patch_DiscreteWeaponComponent_GetFormattedStats
	{
		static void Postfix(ref DiscreteWeaponComponent __instance, ref string __result, ref int groupSize)
		{
			float baseRoundsPerMinute = ShowHiddenStats.calculateRoundsPerMinute(__instance, true);
			float finalRoundsPerMinute = ShowHiddenStats.calculateRoundsPerMinute(__instance);

			//__result = __result + "Rate of Fire";
			//if (groupSize > 1) __result = __result + " (" + groupSize + "x)";
			//__result = __result + ": ";

			//__result = __result + string.Format("{0:0.##} RPM", finalRoundsPerMinute * groupSize);

			float modifier = (finalRoundsPerMinute / baseRoundsPerMinute) - 1.0f;
			//if (modifier != 0.0f) __result = __result + " (" + StatModifier.FormatModifierColored(modifier, false) + ")";

			//__result = __result + "\n";

			string damageText = "";
			bool anyOffensiveAmmo = false;
			foreach (IMunition munition in BundleManager.Instance.AllMunitions)
			{
				if (munition.Type == MunitionType.Ballistic && __instance.IsAmmoCompatible(munition))
				{
					damageText = damageText + string.Format(" - {0}: {1:N0}", munition.MunitionName, finalRoundsPerMinute * groupSize * munition.DamageCharacteristics.ComponentDamage) + " DPS\n";
					anyOffensiveAmmo = true;
				}
			}

			if (!anyOffensiveAmmo)
			{
				return;
			}

			__result = __result + "Sustained Damage";
			if (groupSize > 1) __result = __result + " (" + groupSize + "x)";
			if (modifier != 0.0f) __result = __result + " (" + StatModifier.FormatModifierColored(modifier, false) + ")";
			__result = __result + "\n";
			__result = __result + damageText;

			int magazineSize = (int)Utilities.GetPrivateField(__instance, "_magazineSize");
			if (magazineSize > 1)
			{
				float baseBurstRoundsPerMinute = ShowHiddenStats.calculateRoundsPerMinute(__instance, true, true);
				float finalBurstRoundsPerMinute = ShowHiddenStats.calculateRoundsPerMinute(__instance, false, true);
				float burstModifier = (finalBurstRoundsPerMinute / baseBurstRoundsPerMinute) - 1.0f;

				__result = __result + "Burst Damage";
				if (groupSize > 1) __result = __result + " (" + groupSize + "x)";
				if (burstModifier != 0.0f) __result = __result + " (" + StatModifier.FormatModifierColored(burstModifier, false) + ")";
				__result = __result + "\n";

				foreach (IMunition munition in BundleManager.Instance.AllMunitions)
				{
					if (munition.Type == MunitionType.Ballistic && __instance.IsAmmoCompatible(munition))
					{
						__result = __result + string.Format(" - {0}: {1:N0}", munition.MunitionName, finalBurstRoundsPerMinute * groupSize * munition.DamageCharacteristics.ComponentDamage) + " DPS\n";
					}
				}

				__result = __result + "\n";
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

/*
	[HarmonyPatch(typeof(LightweightMunitionBase), "GetDetailText")]
	class Patch_LightweightMunitionBase_GetDetailText
	{
		static void Postfix(ref LightweightMunitionBase __instance, ref List<ValueTuple<string, string>> __result)
		{
			float armorDamageRadius = (float)Utilities.GetPrivateField(__instance, "_armorDamageRadius");
			float randomEffectMultiplier = (float)Utilities.GetPrivateField(__instance, "_randomEffectMultiplier");
			float overpenDamageMultiplier = (float)Utilities.GetPrivateField(__instance, "_overpenDamageMultiplier");
			float crewVulnerabilityMultiplier = (float)Utilities.GetPrivateField(__instance, "_crewVulnerabilityMultiplier");

			__result.Add(ShowHiddenStats.GetArmorShreddingStat(armorDamageRadius));
			__result.Add(ShowHiddenStats.GetOverpenDamageStat(overpenDamageMultiplier));
			if (randomEffectMultiplier != 1f) __result.Add(ShowHiddenStats.GetCriticalEventStat(randomEffectMultiplier));
			if (crewVulnerabilityMultiplier != 1f) __result.Add(ShowHiddenStats.GetCrewDamageStat(crewVulnerabilityMultiplier));
		}
	}

	[HarmonyPatch(typeof(LightweightSplashingShell), "GetDetailText")]
	class Patch_LightweightSplashingShell_GetDetailText
	{
		static void Postfix(ref LightweightSplashingShell __instance, ref List<ValueTuple<string, string>> __result)
		{
			bool componentDamageFallsOff = (bool)Utilities.GetPrivateField(__instance, "_componentDamageFallsOff");
			AnimationCurve damageFalloff = (AnimationCurve)Utilities.GetPrivateField(__instance, "_damageFalloff");
			float maxFlightTime = (float)Utilities.GetPrivateField(__instance, "_maxFlightTime");
			float flightSpeed = (float)Utilities.GetPrivateField(__instance, "_flightSpeed");

			if (componentDamageFallsOff && damageFalloff != null) __result.AddRange(ShowHiddenStats.GetDamageFalloffStat(damageFalloff, maxFlightTime * flightSpeed));
		}
	}
*/

/*
		public static float CalculateRoundsPerMinute(DiscreteWeaponComponent weapon, bool useBaseStats = false, bool burstOnly = false)
        {
            int magazineSize = (int)Utilities.GetPrivateField(weapon, "_magazineSize");

            if (burstOnly && magazineSize <= 1)
            {
                Debug.LogError("(SHS) error in rate of fire calculation, burst firerate requested on a weapon with no autoloader: " + weapon.WepName);
                return 0;
            }
            
            StatValue statRecycleTime = (StatValue)Utilities.GetPrivateField(weapon, "_statRecycleTime");
            StatValue statReloadTime = (StatValue)Utilities.GetPrivateField(weapon, "_statReloadTime");

            float fullCycleTime;

            if (useBaseStats)
            {
                fullCycleTime = (magazineSize - 1) * statRecycleTime.BaseValue;
                if (!burstOnly) fullCycleTime += statReloadTime.BaseValue;
            }
            else
            {
                fullCycleTime = (magazineSize - 1) * statRecycleTime.Value;
                if (!burstOnly) fullCycleTime += statReloadTime.Value;
            }

            float secondsPerRound = fullCycleTime / magazineSize;
            float roundsPerMinute = 60 / secondsPerRound;

            return roundsPerMinute;
        }

        public static float CalculateRoundsPerMinute(ContinuousWeaponComponent weapon)
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
*/
