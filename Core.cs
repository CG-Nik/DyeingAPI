using Alta;
using Alta.Blacksmithing;
using Alta.Caves;
using Alta.Liquid;
using Alta.Networking;
using Alta.Networking.Servers;
using HarmonyLib;
using MateriaLib;
using MelonLoader;
using NLog.LayoutRenderers.Wrappers;
using System.Drawing;
using System.Reflection;
using System.Security.Policy;
using System.Xml.Linq;
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

            LiquidContent liquidContent = (LiquidContent)typeof(LiquidContainer).GetField("content", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(liquidContainer);
            NetworkPrefab? networkPrefab_item = other.GetComponentInParent<NetworkPrefab>();

            if (networkPrefab_item == null) { return; }
            if (networkPrefab_item.GetComponent<PhysicalMaterialPart>() == null) { return; }
            
            DyeRecipe? dyeRecipe = DyeRecipe.GetValidDyeRecipe(networkPrefab_item.Hash, networkPrefab_item.GetComponent<PhysicalMaterialPart>().PhysicalMaterial.Hash, liquidContent.Liquid.Hash);
            
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
        /// 
        /// </summary>
        /// <param name="inputPrefabs">List of hashes of the NetworkPrefabs that should be allowed to be dyed by this recipe. The only requirement is that it must have a PhysicalMaterialPart directly on the same object as the NetworkPrefab.</param>
        /// <param name="inputMaterials">List of hashes of the PhysicalMaterials that should be whitelisted for this recipe. For example, if you set this to the Dais Leather hash, you can only do this recipe if the NetworkPrefab is already Dais Leather.</param>
        /// <param name="excludeInputMaterials">If true, inputMaterials becomes a blacklist instead of a whitelist.</param>
        /// <param name="liquids">List of hashes of the LiquidDefinitions that should be allowed for this recipe.</param>
        /// <param name="outputMaterial">PhysicalMaterial that the NetworkPrefab will be given by this recipe.</param>
        public DyeRecipe(string name, int hash, List<uint> inputPrefabs, List<uint> inputMaterials, bool excludeInputMaterials, List<uint> liquids, uint outputMaterial)
        {
            this.name = name;
            base.hash = hash;
            this.inputPrefabs = inputPrefabs;
            this.inputMaterials = inputMaterials;
            this.excludeInputMaterials = excludeInputMaterials;
            this.liquids = liquids;
            this.outputMaterial = outputMaterial;

            this.Register();
        }

        public List<uint> inputPrefabs;
        public List<uint> inputMaterials;
        public bool excludeInputMaterials;
        public List<uint> liquids;
        public uint outputMaterial;

        public static DyeRecipe GetValidDyeRecipe(uint prefab, uint material, uint liquid)
        {
            List<DyeRecipe> validRecipes = [];
            foreach (DyeRecipe dyeRecipe in DyeRecipe.All)
            {
                if (dyeRecipe.inputPrefabs.Contains(prefab) && (dyeRecipe.excludeInputMaterials ? !dyeRecipe.inputMaterials.Contains(material) : dyeRecipe.inputMaterials.Contains(material)) && dyeRecipe.liquids.Contains(liquid))
                {
                    validRecipes.Add(dyeRecipe);
                }
            }

            if (validRecipes.Count > 0)
            {
                return validRecipes[0];
            }

            return null;
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
            material_leather.SetVector("_Color", new Vector4(0.6f, 0.6f, 0.6f, 1f));
            material_leather.SetVector("_Color1", new Vector4(0.7f, 0.7f, 0.7f, 1f));
            material_leather.SetVector("_Color2", new Vector4(0.8f, 0.8f, 0.8f, 1f));
            libMaterial_leather.ReplaceAllMaterials(material_leather);

            LibMaterial libMaterial_canvas = new LibMaterial("Colorless Canvas", 10202, LibMaterial.MaterialType.canvas);
            LibMaterial.NewMaterials.Add(libMaterial_canvas);
            libMaterial_canvas.Configure(new MaterialConfig { });
            Material worn = UnityEngine.Object.Instantiate(libMaterial_canvas.physicalMaterial.GetMaterial(PhysicalMaterialChannel.A));
            Material cutout = UnityEngine.Object.Instantiate(libMaterial_canvas.physicalMaterial.GetMaterial(PhysicalMaterialChannel.B));
            worn.name = "Colorless Canvas Worn";
            cutout.name = "Colorless Canvas Cutout";
            worn.SetVector("_ColorA", new Vector4(0.55f, 0.55f, 0.55f, 1f));
            worn.SetVector("_Color", new Vector4(0.625f, 0.625f, 0.625f, 1f));
            cutout.SetVector("_ColorA", new Vector4(0.55f, 0.55f, 0.55f, 1f));
            cutout.SetVector("_Color", new Vector4(0.7f, 0.7f, 0.7f, 1f));
            libMaterial_canvas.ReplaceAllMaterials(worn, cutout);

            LibMaterial libMaterial_rope = new LibMaterial("Colorless Rope", 10203, LibMaterial.MaterialType.rope);
            LibMaterial.NewMaterials.Add(libMaterial_rope);
            libMaterial_rope.Configure(new MaterialConfig { });
            Material material_rope = UnityEngine.Object.Instantiate(libMaterial_rope.physicalMaterial.GetMaterial(PhysicalMaterialChannel.A));
            material_rope.name = "Colorless Rope";
            material_rope.SetVector("_ColorA", new Vector4(0.6f, 0.6f, 0.6f, 1f));
            material_rope.SetVector("_Color", new Vector4(0.7f, 0.7f, 0.7f, 1f));
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
            new DyeRecipe(
                "Colorless Leather Recipe",
                1,
                [23206u, 47760u, 63204u],
                [10201u],
                true,
                [44872u],
                10201u
            );

            new DyeRecipe(
                "Colorless Canvas Recipe",
                2,
                [34570u],
                [10202u],
                true,
                new List<uint>() { 44872u },
                10202u
            );

            new DyeRecipe(
                "Colorless Rope Recipe",
                3,
                new List<uint>() { 43836u },
                new List<uint>() { 10203u },
                true,
                new List<uint>() { 44872u },
                10203u
            );
        }
    }
}