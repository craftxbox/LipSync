using UnityEngine;
using System.Text.RegularExpressions;
using OpenLipSync.Inference.OVRCompat;

namespace LipSync
{
    /// <summary>
    /// This component is used to setup and manage lipsync for an avatar automatically.
    /// Only ever place this on an avatar object
    /// </summary>
    public class LipSyncController : MonoBehaviour
    {
        [SerializeField]
        public string VisemeSIL = string.Empty,
                    VisemePP = string.Empty,
                    VisemeFF = string.Empty,
                    VisemeTH = string.Empty,
                    VisemeDD = string.Empty,
                    VisemeKK = string.Empty,
                    VisemeCH = string.Empty,
                    VisemeSS = string.Empty,
                    VisemeNN = string.Empty,
                    VisemeRR = string.Empty,
                    VisemeAA = string.Empty,
                    VisemeEE = string.Empty,
                    VisemeIH = string.Empty,
                    VisemeOH = string.Empty,
                    VisemeOU = string.Empty;

        [SerializeField]
        public SkinnedMeshRenderer TargetSMR = null!;

        private void Awake()
        {
            if (!TargetSMR)
            {
                if (Configuration.PluginConfig.Instance.AutoSetup)
                    PerformAutoSetup();
                else
                    Plugin.Log?.Error("LipSyncController: No target SkinnedMeshRenderer found, and Autosetup is disabled. If you have added LipSyncController to your avatar manually, you need to configure it or enable auto-setup in the settings!");
            }
            else if (CheckAllTargetVisemesPresent())
                PerformFinalSetup(TargetSMR, [
                    VisemeSIL,
                    VisemePP,
                    VisemeFF,
                    VisemeTH,
                    VisemeDD,
                    VisemeKK,
                    VisemeCH,
                    VisemeSS,
                    VisemeNN,
                    VisemeRR,
                    VisemeAA,
                    VisemeEE,
                    VisemeIH,
                    VisemeOH,
                    VisemeOU
                ]);
        }

        private bool CheckAllTargetVisemesPresent()
        {
            if (TargetSMR == null)
            {
                Plugin.Log?.Error("LipSyncController: No target SkinnedMeshRenderer found! This shouldn't happen...");
                return false;
            }

            if (string.IsNullOrEmpty(VisemeSIL)
                || string.IsNullOrEmpty(VisemePP)
                || string.IsNullOrEmpty(VisemeFF)
                || string.IsNullOrEmpty(VisemeTH)
                || string.IsNullOrEmpty(VisemeDD)
                || string.IsNullOrEmpty(VisemeKK)
                || string.IsNullOrEmpty(VisemeCH)
                || string.IsNullOrEmpty(VisemeSS)
                || string.IsNullOrEmpty(VisemeNN)
                || string.IsNullOrEmpty(VisemeRR)
                || string.IsNullOrEmpty(VisemeAA)
                || string.IsNullOrEmpty(VisemeEE)
                || string.IsNullOrEmpty(VisemeIH)
                || string.IsNullOrEmpty(VisemeOH)
                || string.IsNullOrEmpty(VisemeOU))
            {
                Plugin.Log?.Error("LipSyncController: Not all visemes are configured! Please configure all visemes in the inspector and Re-build your avatar.");
                return false;
            }

            //check for viseme presence on the target smr
            if (TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeSIL) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemePP) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeFF) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeTH) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeDD) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeKK) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeCH) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeSS) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeNN) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeRR) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeAA) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeEE) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeIH) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeOH) == -1
                || TargetSMR.sharedMesh.GetBlendShapeIndex(VisemeOU) == -1)
            {
                Plugin.Log?.Error("LipSyncController: Not all viseme blendshapes are present on the target SkinnedMeshRenderer! Did you provide the right names in the inspector?.");
                return false;
            }

            return true;

        }

        private void PerformAutoSetup()
        {
            SkinnedMeshRenderer[] SMRs = GetComponentsInChildren<SkinnedMeshRenderer>();
            if (SMRs.Length == 1)
            {
                Plugin.Log?.Debug("LipSyncController: One SkinnedMeshRenderer found, checking for viseme presence.");
                SkinnedMeshRenderer SMR = SMRs[0];

                if (SMR.sharedMesh == null)
                {
                    Plugin.Log?.Error("LipSyncController: WTF: SkinnedMeshRenderer has no mesh ?");
                    return;
                }

                // check if it contains any blendshapes named as a viseme
                if (SMR.sharedMesh.blendShapeCount > 0)
                {
                    string[] visemes = FindVisemeShapes(SMR);
                    if (visemes.Length > 0)
                    {
                        Plugin.Log?.Debug("LipSyncController: Visemes found, proceeding with automatic setup.");
                        PerformFinalSetup(SMR, visemes);
                    }
                    else
                    {
                        Plugin.Log?.Error("LipSyncController: No matching viseme blendshapes found, unable to setup lipsync.");
                    }
                }
                else
                {
                    Plugin.Log?.Error("LipSyncController: Avatar has no blendshapes, unable to setup lipsync.");
                }

            }
            else if (SMRs.Length > 1)
            {
                var foundVisemes = false;

                Plugin.Log?.Debug("LipSyncController: Multiple SkinnedMeshRenderers found on avatar. Trying to find if any have Visemes.");
                foreach (SkinnedMeshRenderer SMR in SMRs)
                {
                    if (SMR.sharedMesh == null)
                    {
                        Plugin.Log?.Error("LipSyncController: WTF: SkinnedMeshRenderer '" + SMR.name + "' has no mesh ?");
                        return;
                    }

                    if (SMR.sharedMesh.blendShapeCount > 0)
                    {
                        string[] visemes = FindVisemeShapes(SMR);
                        if (visemes.Length > 0)
                        {
                            Plugin.Log?.Debug("LipSyncController: Visemes found on " + SMR.name + ", proceeding with automatic setup.");
                            PerformFinalSetup(SMR, visemes);
                            foundVisemes = true;
                            break;
                        }
                    }
                    else
                    {
                        Plugin.Log?.Debug("LipSyncController: SMR " + SMR.name + " has no blendshapes, moving on.");
                    }

                }

                if (!foundVisemes)
                {
                    Plugin.Log?.Error("LipSyncController: No matching viseme blendshapes found on any SkinnedMeshRenderer, unable to setup lipsync.");
                }
            }
            else
            {
                Plugin.Log?.Error("LipSyncController: Avatar has no SkinnedMeshRenderers, unable to setup lipsync.");
            }
        }

        private void PerformFinalSetup(SkinnedMeshRenderer SMR, string[] visemes)
        {
            var micInput = gameObject.AddComponent<MicInput>();
            var morphTarget = gameObject.AddComponent<MorphTarget>();
            morphTarget.SMR = SMR;
            morphTarget.BlendshapeTargets = visemes;
            micInput.StartMic();
        }

        private int[] VisemeIndexes(SkinnedMeshRenderer SMR, string[] visemes)
        {
            int[] output = new int[15];
            for (int i = 0; i <= 14; i++)
            {
                output[i] = SMR.sharedMesh.GetBlendShapeIndex(visemes[i]);
            }
            return output;
        }

        private string[] FindVisemeShapes(SkinnedMeshRenderer SMR)
        {

            for (int i = 0; i < SMR.sharedMesh.blendShapeCount; i++)
            {
                string blendShapeName = SMR.sharedMesh.GetBlendShapeName(i);
                // Regex: If string starts with `viseme` followed by one of '.', '-', or '_' (optional), and matches exactly with one of the following:
                // sil, pp, ff, th, dd, kk, ch, ss, nn, rr, a, aa, e, ee, i, ih, o, oh, u, ou
                if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(sil|pp|ff|th|dd|kk|ch|ss|nn|rr|a|aa|e|ee|i|ih|o|oh|u|ou)$", RegexOptions.IgnoreCase))
                {
                    Plugin.Log?.Debug("LipSyncController: Viseme found: " + blendShapeName);
                    VisemeType visemeType = IdentifyViseme(blendShapeName);
                    Plugin.Log?.Debug("LipSyncController: Viseme identified as " + visemeType);
                    switch (visemeType)
                    {
                        case VisemeType.SIL:
                            VisemeSIL = blendShapeName;
                            break;
                        case VisemeType.PP:
                            VisemePP = blendShapeName;
                            break;
                        case VisemeType.FF:
                            VisemeFF = blendShapeName;
                            break;
                        case VisemeType.TH:
                            VisemeTH = blendShapeName;
                            break;
                        case VisemeType.DD:
                            VisemeDD = blendShapeName;
                            break;
                        case VisemeType.KK:
                            VisemeKK = blendShapeName;
                            break;
                        case VisemeType.CH:
                            VisemeCH = blendShapeName;
                            break;
                        case VisemeType.SS:
                            VisemeSS = blendShapeName;
                            break;
                        case VisemeType.NN:
                            VisemeNN = blendShapeName;
                            break;
                        case VisemeType.RR:
                            VisemeRR = blendShapeName;
                            break;
                        case VisemeType.AA:
                            VisemeAA = blendShapeName;
                            break;
                        case VisemeType.EE:
                            VisemeEE = blendShapeName;
                            break;
                        case VisemeType.IH:
                            VisemeIH = blendShapeName;
                            break;
                        case VisemeType.OH:
                            VisemeOH = blendShapeName;
                            break;
                        case VisemeType.OU:
                            VisemeOU = blendShapeName;
                            break;
                        default:
                            Plugin.Log?.Error("LipSyncController: Invalid or unknown viseme? This shouldn't happen.");
                            break;
                    }
                }

            }

            return [
                VisemeSIL,
                VisemePP,
                VisemeFF,
                VisemeTH,
                VisemeDD,
                VisemeKK,
                VisemeCH,
                VisemeSS,
                VisemeNN,
                VisemeRR,
                VisemeAA,
                VisemeEE,
                VisemeIH,
                VisemeOH,
                VisemeOU
            ];
        }

        private VisemeType IdentifyViseme(string blendShapeName)
        {
            if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(sil)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.SIL;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(pp)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.PP;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(ff)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.FF;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(th)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.TH;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(dd)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.DD;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(kk)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.KK;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(ch)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.CH;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(ss)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.SS;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(nn)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.NN;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(rr)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.RR;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(a|aa)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.AA;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(e|ee)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.EE;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(i|ih)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.IH;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(o|oh)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.OH;
            }
            else if (Regex.IsMatch(blendShapeName, "^(?:viseme[._-]?)?(u|ou)$", RegexOptions.IgnoreCase))
            {
                return VisemeType.OU;
            }
            else
            {
                return VisemeType.Unknown;
            }
        }
    }

    public enum VisemeType
    {
        SIL,
        PP,
        FF,
        TH,
        DD,
        KK,
        CH,
        SS,
        NN,
        RR,
        AA,
        EE,
        IH,
        OH,
        OU,
        Unknown
    }
}
