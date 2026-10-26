using Alta;
using Alta.Blacksmithing;
using Alta.Caves;
using Alta.Liquid;
using Alta.Networking;
using Alta.Networking.Servers;
using HarmonyLib;
using MateriaLib;
using MelonLoader;
using System.Reflection;
using UnityEngine;

[assembly: MelonInfo(typeof(DyeingAPI.Core), "DyeingAPI", "1.0.0", "CGNik", null)]
[assembly: MelonGame("Alta", "A Township Tale")]

namespace DyeingAPI
{
    public class OnTriggerEnterPatch
    {
        internal static void Postfix(LiquidContainerTrigger __instance, Collider other)
        {
            LiquidContainer? liquidContainer = __instance.GetComponentInParent<LiquidContainer>();

            if (liquidContainer == null) { return; }

            LiquidContent liquidContent = liquidContainer.ContainedLiquid;

            if (liquidContent == null) { return; }

            NetworkPrefab? networkPrefab_item = other.GetComponentInParent<NetworkPrefab>();

            if (networkPrefab_item == null) { return; }
            if (networkPrefab_item.GetComponent<PhysicalMaterialPart>() == null) { return; }

            uint liquidHash = liquidContent.Liquid != null ? liquidContent.Liquid.Hash : 0u;
            
            DyeRecipe? dyeRecipe = DyeRecipe.GetValidDyeRecipe(networkPrefab_item.Hash, networkPrefab_item.GetComponent<PhysicalMaterialPart>().PhysicalMaterial.Hash, liquidHash, liquidContent.Data);
            
            if (dyeRecipe == null) { return; }

            PhysicalMaterial physicalMaterial = PhysicalMaterial.All.Where(mat => mat.Hash == dyeRecipe.outputMaterial).First();

            MelonLogger.Msg(dyeRecipe.name);
            MelonLogger.Msg(physicalMaterial.name);

            networkPrefab_item.gameObject.GetComponent<PhysicalMaterialPart>().SetMaterial(physicalMaterial);
        }
    }

    public class DyeRecipe : HashedGeneralValue<DyeRecipe>
    {
        /// <summary>
        /// Initializes a DyeRecipe by setting its values and adding it to .All
        /// </summary>
        /// <param name="inputPrefabs">List of hashes of the NetworkPrefabs that should be allowed to be dyed by this recipe. The only requirement is that it must have a PhysicalMaterialPart directly on the same object as the NetworkPrefab.</param>
        /// <param name="inputMaterials">List of hashes of the PhysicalMaterials that should be whitelisted for this recipe. For example, if you set this to the Dais Leather hash, you can only do this recipe if the NetworkPrefab is already Dais Leather.</param>
        /// <param name="excludeInputMaterials">If true, inputMaterials becomes a blacklist instead of a whitelist.</param>
        /// <param name="liquids">List of hashes of the LiquidDefinitions that should be allowed for this recipe.</param>
        /// <param name="outputMaterial">PhysicalMaterial that the NetworkPrefab will be given by this recipe.</param>
        /// <param name="liquidColors">Colors this recipe accepts from a custom liquid, as 0 to 1 RGB. Alpha is ignored. A custom liquid has no LiquidDefinition, so it is matched by LiquidData.TintedLiquidColor instead of by hash. Preset liquids still only match by the liquids list.</param>
        /// <param name="hueTolerance">How far the hue may be from one of liquidColors, in degrees (0 to 180). Hue wraps around, so 0 and 360 are the same red. Ignored when either color is too gray or too dark to have a real hue.</param>
        /// <param name="saturationTolerance">How far the saturation may be, 0 to 1. Ignored when either color is too dark to have a real saturation.</param>
        /// <param name="valueTolerance">How far the brightness (value) may be, 0 to 1. If several recipes match, the closest one wins.</param>
        /// <param name="liquidBlacklist">List of hashes of LiquidDefinitions that should be blacklisted from being allowed, regardless of if they match colors. Mainly intended to be used for blue colors that would otherwise allow Water as a dye. If null, defaults to the empty list.</param>
        public void Initialize(string name, int hash, List<uint> inputPrefabs, List<uint> inputMaterials, bool excludeInputMaterials, List<uint> liquids, uint outputMaterial, List<Color> liquidColors, float hueTolerance = 15f, float saturationTolerance = 0.15f, float valueTolerance = 0.15f, List<uint> liquidBlacklist = null, bool bypassGlobalLiquidBlacklist = false)
        {
            this.name = name;
            base.hash = hash;
            this.inputPrefabs = inputPrefabs;
            this.inputMaterials = inputMaterials;
            this.excludeInputMaterials = excludeInputMaterials;
            this.liquids = liquids;
            this.outputMaterial = outputMaterial;

            if (liquidColors != null)
            {
                this.liquidColors = liquidColors;
            } // defaults to [] if set to null
            this.hueTolerance = hueTolerance;
            this.saturationTolerance = saturationTolerance;
            this.valueTolerance = valueTolerance;
            if (liquidBlacklist != null)
            {
                this.liquidBlacklist = liquidBlacklist;
            } // defaults to [] if set to null
            this.bypassGlobalLiquidBlacklist = bypassGlobalLiquidBlacklist;

            this.Register();
        }

        /// <summary>
        /// Creates, initializes, and returns a new DyeRecipe
        /// </summary>
        /// <param name="inputPrefabs">List of hashes of the NetworkPrefabs that should be allowed to be dyed by this recipe. The only requirement is that it must have a PhysicalMaterialPart directly on the same object as the NetworkPrefab.</param>
        /// <param name="inputMaterials">List of hashes of the PhysicalMaterials that should be whitelisted for this recipe. For example, if you set this to the Dais Leather hash, you can only do this recipe if the NetworkPrefab is already Dais Leather.</param>
        /// <param name="excludeInputMaterials">If true, inputMaterials becomes a blacklist instead of a whitelist.</param>
        /// <param name="liquids">List of hashes of the LiquidDefinitions that should be allowed for this recipe.</param>
        /// <param name="outputMaterial">PhysicalMaterial that the NetworkPrefab will be given by this recipe.</param>
        /// <param name="liquidColors">Colors this recipe accepts from a custom liquid, as 0 to 1 RGB. Alpha is ignored. A custom liquid has no LiquidDefinition, so it is matched by LiquidData.TintedLiquidColor instead of by hash. Preset liquids still only match by the liquids list.</param>        /// <param name="colorTolerance">How far the liquid's color may be from one of the liquidColors, as a straight distance in RGB. If several recipes match, the closest color wins.</param>
        /// <param name="hueTolerance">How far the hue may be from one of liquidColors, in degrees (0 to 180). Hue wraps around, so 0 and 360 are the same red. Ignored when either color is too gray or too dark to have a real hue.</param>
        /// <param name="saturationTolerance">How far the saturation may be, 0 to 1. Ignored when either color is too dark to have a real saturation.</param>
        /// <param name="valueTolerance">How far the brightness (value) may be, 0 to 1. If several recipes match, the closest one wins.</param>
        /// <param name="liquidBlacklist">List of hashes of LiquidDefinitions that should be blacklisted from being allowed, regardless of if they match colors. Mainly intended to be used for blue colors that would otherwise allow Water as a dye. If null, defaults to the empty list.</param>
        public static DyeRecipe Create(string name, int hash, List<uint> inputPrefabs, List<uint> inputMaterials, bool excludeInputMaterials, List<uint> liquids, uint outputMaterial, List<Color> liquidColors = null, float hueTolerance = 15f, float saturationTolerance = 0.15f, float valueTolerance = 0.15f, List<uint> liquidBlacklist = null, bool bypassGlobalLiquidBlacklist = false)
        {
            DyeRecipe dyeRecipe = ScriptableObject.CreateInstance<DyeRecipe>();
            dyeRecipe.Initialize(name, hash, inputPrefabs, inputMaterials, excludeInputMaterials, liquids, outputMaterial, liquidColors, hueTolerance, saturationTolerance, valueTolerance, liquidBlacklist, bypassGlobalLiquidBlacklist);
            return dyeRecipe;
        }

        public List<uint> inputPrefabs;
        public List<uint> inputMaterials;
        public bool excludeInputMaterials;
        public List<uint> liquids;
        public uint outputMaterial;
        public List<Color> liquidColors = [];
        public float hueTolerance = 15f;
        public float saturationTolerance = 0.15f;
        public float valueTolerance = 0.15f;
        public List<uint> liquidBlacklist = [];
        public bool bypassGlobalLiquidBlacklist = false;

        private const float MinChromaForHue = 0.1f;
        private const float MinValueForSaturation = 0.1f;

        public static List<uint> globalLiquidBlacklist = [44872u];

        public static DyeRecipe GetValidDyeRecipe(uint prefab, uint material, uint liquidDefinition, LiquidData liquidData)
        {
            DyeRecipe? closestColorRecipe = null;
            float closestScore = float.MaxValue;

            foreach (DyeRecipe dyeRecipe in DyeRecipe.All)
            {
                if (!dyeRecipe.inputPrefabs.Contains(prefab)) { continue; }
                if (dyeRecipe.excludeInputMaterials ? dyeRecipe.inputMaterials.Contains(material) : !dyeRecipe.inputMaterials.Contains(material)) { continue; }
                if (liquidDefinition != 0u)
                {
                    if (dyeRecipe.liquids.Contains(liquidDefinition))
                    {
                        return dyeRecipe;
                    } else if (dyeRecipe.liquidBlacklist.Contains(liquidDefinition)
                        || (DyeRecipe.globalLiquidBlacklist.Contains(liquidDefinition) && !dyeRecipe.bypassGlobalLiquidBlacklist))
                    {
                        continue;
                    }
                }
                if (liquidData == null) { continue; }
                if (liquidData.LiquidVisual == null) { continue; }

                foreach (Color color in dyeRecipe.liquidColors)
                {
                    float score = ColorMatchScore(liquidData.TintedLiquidColor, color, dyeRecipe);
                    if (score >= 0f && score < closestScore)
                    {
                        closestScore = score;
                        closestColorRecipe = dyeRecipe;
                    }
                }
            }

            return closestColorRecipe;
        }

        private static float ColorMatchScore(Color liquid, Color target, DyeRecipe dyeRecipe)
        {
            Color.RGBToHSV(liquid, out float liquidHue, out float liquidSaturation, out float liquidValue);
            Color.RGBToHSV(target, out float targetHue, out float targetSaturation, out float targetValue);

            float valueDiff = Mathf.Abs(liquidValue - targetValue);
            if (valueDiff > dyeRecipe.valueTolerance) { return -1f; }
            float score = Normalized(valueDiff, dyeRecipe.valueTolerance);

            if (liquidValue >- MinValueForSaturation && targetValue >= MinValueForSaturation)
            {
                float saturationDiff = Mathf.Abs(liquidSaturation - targetSaturation);
                if (saturationDiff > dyeRecipe.saturationTolerance) { return -1f; }
                score += Normalized(saturationDiff, dyeRecipe.saturationTolerance);

                if (liquidSaturation >= MinChromaForHue && targetSaturation >= MinChromaForHue)
                {
                    float hueDiff = Mathf.Abs(liquidHue - targetHue);
                    if (hueDiff > 0.5f) { hueDiff = 1f - hueDiff; }
                    float hueTolerance = dyeRecipe.hueTolerance / 360f;
                    if (hueDiff > hueTolerance) { return -1f; }
                    score += Normalized(hueDiff, hueTolerance);
                }
            }
            return score;
        }

        private static float Normalized(float diff, float tolerance)
        {
            if (tolerance <= 0f) { return 0f; }
            float ratio = diff / tolerance;
            return ratio * ratio;
        }

        public void Register()
        {
            DyeRecipe.CheckItems();
            Dictionary<uint, DyeRecipe> items = (Dictionary<uint, DyeRecipe>)typeof(HashedGeneralValue<DyeRecipe>).GetField("items", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            items.Add(this.Hash, this);
        }
    }

    public class Core : MelonMod
    {
        public static event Action PreSetUpDyeRecipes = () => { };
        public static event Action SetUpDyeRecipes = () => { };
        public static event Action PostSetUpDyeRecipes = () => { };

        public static List<uint> leatherPrefabHashes = [23206u, 47760u, 63204u];
        public static List<uint> canvasPrefabHashes = [34570u];
        public static List<uint> ropePrefabHashes = [43836u];

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");

            ServerHandler.Initialized += ServerInitialized;
            MateriaLib.Main.SetupMaterial += SetupMaterial;
            MateriaLib.Main.PostSetupMaterial += PostSetupMaterial;
            SetUpDyeRecipes += SetUpColorlessDyeRecipes;
        }

        private void ServerInitialized(ServerHandler serverHandler)
        {
            HarmonyInstance.Patch(AccessTools.Method(typeof(LiquidContainerTrigger), "OnTriggerEnter"), postfix: new HarmonyMethod(typeof(OnTriggerEnterPatch), nameof(OnTriggerEnterPatch.Postfix)));
        }

        private void SetupMaterial()
        {
            LibMaterial libMaterial_leather = new LibMaterial("Colorless Leather", 10201, LibMaterial.MaterialType.leather);
            LibMaterial.NewMaterials.Add(libMaterial_leather);
            libMaterial_leather.Configure(new MaterialConfig { });
            Material material_leather = UnityEngine.Object.Instantiate(libMaterial_leather.physicalMaterial.GetMaterial(PhysicalMaterialChannel.A));
            material_leather.name = "Colorless Leather";
            material_leather.SetVector("_Color", new Vector4(0.3f, 0.3f, 0.3f, 1f));
            material_leather.SetVector("_Color1", new Vector4(0.5f, 0.5f, 0.5f, 1f));
            material_leather.SetVector("_Color2", new Vector4(0.7f, 0.7f, 0.7f, 1f));
            libMaterial_leather.ReplaceAllMaterials(material_leather);

            LibMaterial libMaterial_canvas = new LibMaterial("Colorless Canvas", 10202, LibMaterial.MaterialType.canvas);
            LibMaterial.NewMaterials.Add(libMaterial_canvas);
            libMaterial_canvas.Configure(new MaterialConfig { });
            Material worn = UnityEngine.Object.Instantiate(libMaterial_canvas.physicalMaterial.GetMaterial(PhysicalMaterialChannel.A));
            Material cutout = UnityEngine.Object.Instantiate(libMaterial_canvas.physicalMaterial.GetMaterial(PhysicalMaterialChannel.B));
            worn.name = "Colorless Canvas Worn";
            cutout.name = "Colorless Canvas Cutout";
            worn.SetVector("_ColorA", new Vector4(0.5f, 0.5f, 0.5f, 1f));
            worn.SetVector("_Color", new Vector4(0.55f, 0.55f, 0.55f, 1f));
            cutout.SetVector("_ColorA", new Vector4(0.5f, 0.5f, 0.5f, 1f));
            cutout.SetVector("_Color", new Vector4(0.6f, 0.6f, 0.6f, 1f));
            libMaterial_canvas.ReplaceAllMaterials(worn, cutout);

            LibMaterial libMaterial_rope = new LibMaterial("Colorless Rope", 10203, LibMaterial.MaterialType.rope);
            LibMaterial.NewMaterials.Add(libMaterial_rope);
            libMaterial_rope.Configure(new MaterialConfig { });
            Material material_rope = UnityEngine.Object.Instantiate(libMaterial_rope.physicalMaterial.GetMaterial(PhysicalMaterialChannel.A));
            material_rope.name = "Colorless Rope";
            material_rope.SetVector("_ColorA", new Vector4(0.3f, 0.3f, 0.3f, 1f));
            material_rope.SetVector("_Color", new Vector4(0.5f, 0.5f, 0.5f, 1f));
            libMaterial_rope.ReplaceAllMaterials(material_rope);
        }

        private void PostSetupMaterial()
        {
            PreSetUpDyeRecipes.Invoke();

            SetUpDyeRecipes.Invoke();

            PostSetUpDyeRecipes.Invoke();
        }

        private void SetUpColorlessDyeRecipes()
        {
            DyeRecipe.Create(
                "Colorless Leather Recipe",
                1,
                leatherPrefabHashes,
                [10201u],
                true,
                [44872u],
                10201u,
                bypassGlobalLiquidBlacklist: true
            );

            DyeRecipe.Create(
                "Colorless Canvas Recipe",
                2,
                canvasPrefabHashes,
                [10202u],
                true,
                [44872u],
                10202u,
                bypassGlobalLiquidBlacklist: true
            );

            DyeRecipe.Create(
                "Colorless Rope Recipe",
                3,
                ropePrefabHashes,
                [10203u],
                true,
                [44872u],
                10203u,
                bypassGlobalLiquidBlacklist: true
            );
        }
    }
}