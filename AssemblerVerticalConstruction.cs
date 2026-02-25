using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using crecheng.DSPModSave;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AssemblerVerticalConstruction
{
    [BepInPlugin("lltcggie.DSP.plugin.AssemblerVerticalConstruction", "AssemblerVerticalConstruction", "1.1.9")]
    [BepInDependency(DSPModSavePlugin.MODGUID)]
    [ModSaveSettings(LoadOrder = LoadOrder.Postload)]
    public class AssemblerVerticalConstruction : BaseUnityPlugin, IModCanSave
    {
        public static readonly int CurrentSavedataVersion = 3;

        public static ConfigEntry<bool> IsResetNextIds;

        private static ManualLogSource _logger;
        public static new ManualLogSource Logger { get => _logger; }

        AssemblerVerticalConstruction()
        {
            _logger = base.Logger;
        }

        ~AssemblerVerticalConstruction()
        {
            if (IsResetNextIds.Value == true)
            {
                IsResetNextIds.Value = false;
                Config.Save();
            }
        }

        public void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

            IsResetNextIds = Config.Bind("config", "IsResetNextIds", false, "true if building overlay relationships must be recalculated when loading save data. This value is always reset to false when the game is closed.");
        }

        public void Import(BinaryReader binaryReader)
        {
            if (DSPGame.Game == null)
            {
                return;
            }

            if (IsResetNextIds.Value)
            {
                Logger.LogInfo("ResetNextIds");
                AssemblerPatches.ResetNextIds();
                return;
            }

            var version = binaryReader.ReadInt32() * -1; // 为了避免被误认为是 assemblerCapacity，应使用负数表示（因为正数可能会引起混淆）
            if (version < CurrentSavedataVersion)
            {
                Logger.LogInfo(string.Format("Old save data version: read {0} current {1}", version, CurrentSavedataVersion));
                Logger.LogInfo("ResetNextIds");
                AssemblerPatches.ResetNextIds();
                return;
            }
            else if (version != CurrentSavedataVersion)
            {
                Logger.LogWarning(string.Format("Invalid save data version: read {0} current {1}", version, CurrentSavedataVersion));
                Logger.LogInfo("ResetNextIds");
                AssemblerPatches.ResetNextIds();
                return;
            }

            var assemblerCapacity = binaryReader.ReadInt32();

            if (assemblerCapacity > AssemblerPatches.assemblerComponentEx.assemblerCapacity)
            {
                AssemblerPatches.assemblerComponentEx.SetAssemblerCapacity(assemblerCapacity);
            }

            for (int i = 0; i < assemblerCapacity; i++)
            {
                var num = binaryReader.ReadInt32();
                for (int j = 0; j < num; j++)
                {
                    var nextId = binaryReader.ReadInt32();
                    AssemblerPatches.assemblerComponentEx.SetAssemblerNext(i, j, nextId);
                }
            }
        }

        public void Export(BinaryWriter binaryWriter)
        {
            if (DSPGame.Game == null)
            {
                return;
            }

            binaryWriter.Write(CurrentSavedataVersion * -1); // 正数だとassemblerCapacityと誤認する恐れがあるので負数で扱う

            binaryWriter.Write(AssemblerPatches.assemblerComponentEx.assemblerCapacity);
            for (int i = 0; i < AssemblerPatches.assemblerComponentEx.assemblerCapacity; i++)
            {
                if (AssemblerPatches.assemblerComponentEx.assemblerNextIds[i] != null)
                {
                    binaryWriter.Write(AssemblerPatches.assemblerComponentEx.assemblerNextIds[i].Length);
                    for (int j = 0; j < AssemblerPatches.assemblerComponentEx.assemblerNextIds[i].Length; j++)
                    {
                        binaryWriter.Write(AssemblerPatches.assemblerComponentEx.assemblerNextIds[i][j]);
                    }
                }
                else
                {
                    binaryWriter.Write(0);
                }
            }
        }

        public void IntoOtherSave()
        {
            if (DSPGame.Game == null || DSPGame.IsMenuDemo) // 即使在标题画面的演示工厂加载时也会被调用，但由于此时并未使用本MOD的功能，为节省开销，避免调用 ResetNextIds()
            {
                return;
            }

            Logger.LogInfo("ResetNextIds");

            AssemblerPatches.ResetNextIds();
        }
    }

    [HarmonyPatch]
    internal class AssemblerPatches
    {
        class ModelSetting
        {
            public bool multiLevelAllowPortsOrSlots;
            public List<int> multiLevelAlternativeIds;
            public List<bool> multiLevelAlternativeYawTransposes;
            public Vector3 lapJoint;

            // 各垂直建设研究等级下允许的最大垂直建筑数量
            // 设定时已考虑确保在达到最大值时不会超出行星护盾范围
            // [0] 为初始值，[1] 为完成垂直建设等级1研究后的数值，[6] 为完成最高等级（等级6）垂直建设研究后的数值
            public int[] multiLevelMaxBuildCount;

            public ModelSetting(bool multiLevelAllowPortsOrSlots, List<int> multiLevelAlternativeIds, List<bool> multiLevelAlternativeYawTransposes, Vector3 lapJoint, int[] multiLevelMaxBuildCount)
            {
                this.multiLevelAllowPortsOrSlots = multiLevelAllowPortsOrSlots;
                this.multiLevelAlternativeIds = multiLevelAlternativeIds;
                this.multiLevelAlternativeYawTransposes = multiLevelAlternativeYawTransposes;
                this.lapJoint = lapJoint;
                this.multiLevelMaxBuildCount = multiLevelMaxBuildCount;
            }
        }

        // 制造台
        readonly static ModelSetting AssemblerSetting = new ModelSetting(
            false,
            new List<int> { 2303, 2304, 2305, 2318 },
            new List<bool> { false, false, false, false },
            new Vector3(0, 5.05f, 0),
            new int[7] { 2, 4, 6, 8, 10, 11, 12 }
            );
        // 熔炉
        readonly static ModelSetting SmelterSetting = new ModelSetting(
            false,
            new List<int> { 2302, 2315, 2319, 6230 },
            new List<bool> { false, false, false, false },
            new Vector3(0, 4.3f, 0),
            new int[7] { 2, 4, 6, 8, 10, 12, 14 }
            );
        // 化工厂
        readonly static ModelSetting ChemicalPlantSetting = new ModelSetting(
            false,
            new List<int> { 2309, 2317 },
            new List<bool> { false, false },
            new Vector3(0, 6.85f, 0),
            new int[7] { 2, 4, 5, 6, 7, 8, 9 }
            );
        // 原油精炼厂
        readonly static ModelSetting OilRefinerySetting = new ModelSetting(
            false,
            new List<int> { 2308 },
            new List<bool> { false },
            new Vector3(0, 11.8f, 0),
            new int[7] { 1, 2, 2, 3, 3, 4, 5 }
            );
        // 微型粒子对撞机
        readonly static ModelSetting MiniatureParticleColliderSetting = new ModelSetting(
            false,
            new List<int> { 2310 },
            new List<bool> { false },
            new Vector3(0, 15.2f, 0),
            new int[7] { 1, 2, 2, 3, 3, 3, 4 }
            );

        readonly static Dictionary<int, ModelSetting> ModelSettingDict = new Dictionary<int, ModelSetting>()
        {
            { 2303, AssemblerSetting }, // 制造台 Mk.I
            { 2304, AssemblerSetting }, // 制造台 Mk.II
            { 2305, AssemblerSetting }, // 制造台 Mk.III
            { 2318, AssemblerSetting }, // 重组式制造台

            { 2302, SmelterSetting }, // 电弧熔炉
            { 2315, SmelterSetting }, // 位面熔炉
            { 2319, SmelterSetting }, // 负熵熔炉
            { 6230, SmelterSetting }, // ProjectGenesis 熔炉

            { 2309, ChemicalPlantSetting }, // 化工厂
            { 2317, ChemicalPlantSetting }, // 量子化工厂

            { 2308, OilRefinerySetting }, // 原油精炼厂

            { 2310, MiniatureParticleColliderSetting }, // 微型粒子对撞机
        };

        public static AssemblerComponentEx assemblerComponentEx = new AssemblerComponentEx();

        public static void ResetNextIds()
        {
            for (int i = 0; i < GameMain.data.factories.Length; i++)
            {
                if (GameMain.data.factories[i] == null)
                {
                    continue;
                }

                var _this = GameMain.data.factories[i].factorySystem;
                if (_this == null)
                {
                    continue;
                }

                var factoryIndex = _this.factory.index;
                int[] assemblerPrevIds = new int[assemblerComponentEx.assemblerNextIds[factoryIndex].Length];

                var assemblerCapacity = Traverse.Create(_this).Field("assemblerCapacity").GetValue<int>();
                for (int j = 1; j < assemblerCapacity; j++)
                {
                    var assemblerId = j;

                    int entityId = _this.assemblerPool[assemblerId].entityId;
                    if (entityId == 0)
                    {
                        continue;
                    }

                    int nextEntityId = entityId;
                    do
                    {
                        int prevEntityId = nextEntityId;

                        bool isOutput;
                        int otherObjId;
                        int otherSlot;
                        _this.factory.ReadObjectConn(nextEntityId, PlanetFactory.kMultiLevelOutputSlot, out isOutput, out otherObjId, out otherSlot);

                        nextEntityId = otherObjId;

                        if (nextEntityId > 0)
                        {
                            int prevAssemblerId = _this.factory.entityPool[prevEntityId].assemblerId;
                            int nextAssemblerId = _this.factory.entityPool[nextEntityId].assemblerId;
                            if (nextAssemblerId > 0 && _this.assemblerPool[nextAssemblerId].id == nextAssemblerId)
                            {
                                assemblerComponentEx.SetAssemblerNext(factoryIndex, prevAssemblerId, nextAssemblerId);
                                assemblerPrevIds[nextAssemblerId] = prevAssemblerId;
                            }
                        }
                    }
                    while (nextEntityId != 0);
                }

                // 设置配方（与最下方装配器的配方保持一致）
                var lenAssemblerPrevIds = assemblerPrevIds.Length;
                for (int j = 1; j < lenAssemblerPrevIds; j++)
                {
                    var assemblerPrevId = assemblerPrevIds[j];
                    if (assemblerPrevIds[j] == 0 && _this.assemblerPool[assemblerPrevId].id == assemblerPrevId)
                    {
                        // 找到根节点后，从该节点开始遍历其子节点并设置配方
                        var assemblerNextId = assemblerComponentEx.GetNextId(factoryIndex, j);
                        while (assemblerNextId != 0)
                        {
                            AssemblerComponentEx.FindRecipeIdForBuild(_this, assemblerNextId);
                            assemblerNextId = assemblerComponentEx.GetNextId(factoryIndex, assemblerNextId);
                        }
                    }
                }
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ItemProto), "Preload")]
        private static void PreloadPatch(ItemProto __instance, int _index)
        {
            ModelProto modelProto = LDB.models.modelArray[__instance.ModelIndex];
            if (modelProto != null && modelProto.prefabDesc != null && modelProto.prefabDesc.isAssembler == true)
            {
                ModelSetting setting;
                if (ModelSettingDict.TryGetValue(__instance.ID, out setting))
                {
                    LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevel = true;
                    LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAllowPortsOrSlots = setting.multiLevelAllowPortsOrSlots;
                    LDB.models.modelArray[__instance.ModelIndex].prefabDesc.lapJoint = setting.lapJoint;

                    // multiLevelAlternative 中不包含自身的 ID，因此无需排除
                    var index = setting.multiLevelAlternativeIds.FindIndex(item => item == __instance.ID);
                    if (index >= 0)
                    {
                        var multiLevelAlternativeIds = new int[setting.multiLevelAlternativeIds.Count - 1];
                        var multiLevelAlternativeYawTransposes = new bool[setting.multiLevelAlternativeIds.Count - 1];

                        int count = 0;
                        for (int i = 0; i < setting.multiLevelAlternativeIds.Count; i++)
                        {
                            if (i == index)
                            {
                                continue;
                            }

                            multiLevelAlternativeIds[count] = setting.multiLevelAlternativeIds[i];
                            multiLevelAlternativeYawTransposes[count] = setting.multiLevelAlternativeYawTransposes[i];
                            count++;
                        }

                        LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAlternativeIds = multiLevelAlternativeIds;
                        LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAlternativeYawTransposes = multiLevelAlternativeYawTransposes;
                    }
                    else
                    {
                        LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAlternativeIds = setting.multiLevelAlternativeIds.ToArray();
                        LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAlternativeYawTransposes = setting.multiLevelAlternativeYawTransposes.ToArray();
                    }
                }
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(FactorySystem), "SetAssemblerCapacity")]
        private static bool SetAssemblerCapacityPatch(FactorySystem __instance, int newCapacity)
        {
            var index = __instance.factory.index;
            if (index > assemblerComponentEx.assemblerNextIds.Length)
            {
                assemblerComponentEx.SetAssemblerCapacity(assemblerComponentEx.assemblerCapacity * 2);
            }

            var assemblerCapacity = Traverse.Create(__instance).Field("assemblerCapacity").GetValue<int>();

            int[] oldAssemblerNextIds = assemblerComponentEx.assemblerNextIds[index];
            assemblerComponentEx.assemblerNextIds[index] = new int[newCapacity];
            if (oldAssemblerNextIds != null)
            {
                Array.Copy(oldAssemblerNextIds, assemblerComponentEx.assemblerNextIds[index], (newCapacity <= assemblerCapacity) ? newCapacity : assemblerCapacity);
            }

            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PlanetFactory), "ApplyInsertTarget")]
        public static bool ApplyInsertTargetPatch(PlanetFactory __instance, int entityId, int insertTarget, int slotId, int offset)
        {
            if (entityId != 0)
            {
                if (insertTarget < 0)
                {
                    Assert.CannotBeReached();
                    insertTarget = 0;
                }
                else
                {
                    // 备注：该方法可能由 PlanetFactory.ApplyEntityOutput() 或 PlanetFactory.ApplyEntityInput() 调用，
                    // 在这两种情况下，entityId 与 insertTarget 会互换位置，
                    // 因此必须判断哪个在上方、哪个在下方。
                    // 本程序假设 insertTarget 位于上方（即 next）
                    bool isOutput;
                    int otherObjId;
                    int otherSlot;
                    __instance.ReadObjectConn(entityId, PlanetFactory.kMultiLevelOutputSlot, out isOutput, out otherObjId, out otherSlot);
                    if (!(isOutput && otherObjId == insertTarget))
                    {
                        // Swap
                        int temp = insertTarget;
                        insertTarget = entityId;
                        entityId = temp;
                    }

                    int assemblerId = __instance.entityPool[entityId].assemblerId;
                    if (assemblerId > 0 && __instance.entityPool[insertTarget].assemblerId > 0)
                    {
                        assemblerComponentEx.SetAssemblerInsertTarget(__instance, assemblerId, insertTarget);
                    }
                }
            }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PlanetFactory), "ApplyEntityDisconnection")]
        public static bool ApplyEntityDisconnectionPatch(PlanetFactory __instance, int otherEntityId, int removingEntityId, int otherSlotId, int removingSlotId)
        {
            if (otherEntityId == 0)
            {
                return true;
            }

            var _this = __instance;
            int assemblerId = _this.entityPool[otherEntityId].assemblerId;
            if (assemblerId > 0)
            {
                int assemblerRemoveId = _this.entityPool[removingEntityId].assemblerId;
                if (assemblerRemoveId > 0)
                {
                    assemblerComponentEx.UnsetAssemblerInsertTarget(__instance, assemblerId, assemblerRemoveId);
                }
            }
            return true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(PlanetFactory), "CreateEntityLogicComponents")]
        public static void CreateEntityLogicComponentsPatch(PlanetFactory __instance, int entityId, PrefabDesc desc, int prebuildId)
        {
            if (entityId == 0 || !desc.isAssembler)
            {
                return;
            }

            // 蓝图建筑放置后重新设置配方
            // 备注：如果是蓝图建筑，在 ApplyInsertTarget() 之后，配方会被蓝图建筑的配方覆盖，因此需要在此处重新设置
            int assemblerId = __instance.entityPool[entityId].assemblerId;
            AssemblerComponentEx.FindRecipeIdForBuild(__instance.factorySystem, assemblerId);
        }

        // 注意：_assembler_parallel 是 GameLogic 类的私有方法
        [HarmonyPostfix, HarmonyPatch(typeof(GameLogic), "_assembler_parallel")]
        public static void AssemblerParallelPatch(GameLogic __instance, int threadOrdinal)
        {
            // 1. 获取当前线程的任务上下文
            var assemblerContext = __instance.threadController.gameThreadContext.assembler;
            ref var threadContext = ref assemblerContext.threadContexts[threadOrdinal];

            var factories = Traverse.Create(__instance).Field<PlanetFactory[]>("factories").Value;
            var factoryCount = Traverse.Create(__instance).Field<int>("factoryCount").Value;
            if (factoryCount <= 0)
            {
                return;
            }

            // 2. 遍历当前线程负责的所有工厂批次
            for (int batch = threadContext.batchBegin; batch <= threadContext.batchEnd; batch++)
            {
                int actualFactoryIndex = (batch + assemblerContext.batchOffset) % factoryCount;
                var planetFactory = factories[actualFactoryIndex];
                var factorySystem = planetFactory.factorySystem;
                var assemblerPool = factorySystem.assemblerPool;
                var assemblerCursor = factorySystem.assemblerCursor;

                // 3. 确定当前批次中需要处理的组装机ID范围
                int startId = (batch == threadContext.batchBegin) ? threadContext.idBegin : 1;
                int endId = (batch == threadContext.batchEnd) ? threadContext.idEnd : assemblerCursor;

                // 4. 对每个有效的组装机执行物流更新
                for (int i = startId; i < endId; i++)
                {
                    if (assemblerPool[i].id == i)
                    {
                        var nextId = assemblerComponentEx.GetNextId(actualFactoryIndex, i);
                        if (nextId > 0)
                        {
                            // 在多线程环境下，必须使用互斥锁
                            assemblerComponentEx.UpdateOutputToNext(planetFactory, actualFactoryIndex, i, assemblerPool, nextId, true);
                        }
                    }
                }
            }
        }

        public static void SyncAssemblerFunctions(FactorySystem factorySystem, Player player, int assemblerId)
        {
            var _this = factorySystem;
            int entityId = _this.assemblerPool[assemblerId].entityId;
            if (entityId == 0)
            {
                return;
            }

            int num = entityId;
            do
            {
                bool flag;
                int num3;
                int num4;
                _this.factory.ReadObjectConn(num, PlanetFactory.kMultiLevelInputSlot, out flag, out num3, out num4);
                num = num3;
                if (num > 0)
                {
                    int assemblerId2 = _this.factory.entityPool[num].assemblerId;
                    if (assemblerId2 > 0 && _this.assemblerPool[assemblerId2].id == assemblerId2)
                    {
                        if (_this.assemblerPool[assemblerId].recipeId > 0)
                        {
                            if (_this.assemblerPool[assemblerId2].recipeId != _this.assemblerPool[assemblerId].recipeId)
                            {
                                _this.TakeBackItems_Assembler(player, assemblerId2);
                                _this.assemblerPool[assemblerId2].SetRecipe(_this.assemblerPool[assemblerId].recipeId, _this.factory.entitySignPool);
                            }
                        }
                        else if (_this.assemblerPool[assemblerId2].recipeId != 0)
                        {
                            _this.TakeBackItems_Assembler(player, assemblerId2);
                            _this.assemblerPool[assemblerId2].SetRecipe(0, _this.factory.entitySignPool);
                        }
                    }
                }
            }
            while (num != 0);

            num = entityId;
            do
            {
                bool flag;
                int num3;
                int num4;
                _this.factory.ReadObjectConn(num, PlanetFactory.kMultiLevelOutputSlot, out flag, out num3, out num4);
                num = num3;
                if (num > 0)
                {
                    int assemblerId3 = _this.factory.entityPool[num].assemblerId;
                    if (assemblerId3 > 0 && _this.assemblerPool[assemblerId3].id == assemblerId3)
                    {
                        if (_this.assemblerPool[assemblerId].recipeId > 0)
                        {
                            if (_this.assemblerPool[assemblerId3].recipeId != _this.assemblerPool[assemblerId].recipeId)
                            {
                                _this.TakeBackItems_Assembler(_this.factory.gameData.mainPlayer, assemblerId3);
                                _this.assemblerPool[assemblerId3].SetRecipe(_this.assemblerPool[assemblerId].recipeId, _this.factory.entitySignPool);
                            }
                        }
                        else if (_this.assemblerPool[assemblerId3].recipeId != 0)
                        {
                            _this.TakeBackItems_Assembler(_this.factory.gameData.mainPlayer, assemblerId3);
                            _this.assemblerPool[assemblerId3].SetRecipe(0, _this.factory.entitySignPool);
                        }
                    }
                }
            }
            while (num != 0);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIAssemblerWindow), "OnRecipeResetClick")]
        public static void OnRecipeResetClickPatch(UIAssemblerWindow __instance)
        {
            if (__instance.assemblerId == 0 || __instance.factory == null)
            {
                return;
            }
            AssemblerComponent assemblerComponent = __instance.factorySystem.assemblerPool[__instance.assemblerId];
            if (assemblerComponent.id != __instance.assemblerId)
            {
                return;
            }
            SyncAssemblerFunctions(__instance.factorySystem, __instance.player, __instance.assemblerId);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIAssemblerWindow), "OnRecipePickerReturn")]
        public static void OnRecipePickerReturnPatch(UIAssemblerWindow __instance)
        {
            if (__instance.assemblerId == 0 || __instance.factory == null)
            {
                return;
            }
            AssemblerComponent assemblerComponent = __instance.factorySystem.assemblerPool[__instance.assemblerId];
            if (assemblerComponent.id != __instance.assemblerId)
            {
                return;
            }
            SyncAssemblerFunctions(__instance.factorySystem, __instance.player, __instance.assemblerId);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIAssemblerWindow), "OnIncSwitchClick")]
        public static void OnIncSwitchClickPatch(UIAssemblerWindow __instance)
        {
            if (__instance.assemblerId == 0 || __instance.factory == null)
            {
                return;
            }
            AssemblerComponent assemblerComponent = __instance.factorySystem.assemblerPool[__instance.assemblerId];
            if (assemblerComponent.id != __instance.assemblerId)
            {
                return;
            }
            // 同步 forceAccMode 到所有堆叠的装配器
            SyncForceAccMode(__instance.factorySystem, __instance.assemblerId);
        }

        public static void SyncForceAccMode(FactorySystem factorySystem, int assemblerId)
        {
            var _this = factorySystem;
            int entityId = _this.assemblerPool[assemblerId].entityId;
            if (entityId == 0)
            {
                return;
            }

            bool forceAccMode = _this.assemblerPool[assemblerId].forceAccMode;

            // 向下遍历同步
            int num = entityId;
            do
            {
                bool flag;
                int num3;
                int num4;
                _this.factory.ReadObjectConn(num, PlanetFactory.kMultiLevelInputSlot, out flag, out num3, out num4);
                num = num3;
                if (num > 0)
                {
                    int assemblerId2 = _this.factory.entityPool[num].assemblerId;
                    if (assemblerId2 > 0 && _this.assemblerPool[assemblerId2].id == assemblerId2)
                    {
                        RecipeExecuteData recipeExecuteData = _this.assemblerPool[assemblerId2].recipeExecuteData;
                        if (recipeExecuteData != null && recipeExecuteData.productive)
                        {
                            _this.assemblerPool[assemblerId2].forceAccMode = forceAccMode;
                        }
                    }
                }
            }
            while (num != 0);

            // 向上遍历同步
            num = entityId;
            do
            {
                bool flag;
                int num3;
                int num4;
                _this.factory.ReadObjectConn(num, PlanetFactory.kMultiLevelOutputSlot, out flag, out num3, out num4);
                num = num3;
                if (num > 0)
                {
                    int assemblerId3 = _this.factory.entityPool[num].assemblerId;
                    if (assemblerId3 > 0 && _this.assemblerPool[assemblerId3].id == assemblerId3)
                    {
                        RecipeExecuteData recipeExecuteData = _this.assemblerPool[assemblerId3].recipeExecuteData;
                        if (recipeExecuteData != null && recipeExecuteData.productive)
                        {
                            _this.assemblerPool[assemblerId3].forceAccMode = forceAccMode;
                        }
                    }
                }
            }
            while (num != 0);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(BuildingParameters), "PasteToFactoryObject")]
        public static void PasteToFactoryObjectPatch(BuildingParameters __instance, int objectId, PlanetFactory factory)
        {
            if (objectId <= 0)
            {
                return;
            }

            int assemblerId = factory.entityPool[objectId].assemblerId;
            if (assemblerId != 0 && __instance.type == BuildingType.Assembler && factory.factorySystem.assemblerPool[assemblerId].recipeId == __instance.recipeId)
            {
                ItemProto itemProto = LDB.items.Select((int)factory.entityPool[objectId].protoId);
                if (itemProto != null && itemProto.prefabDesc != null)
                {
                    SyncAssemblerFunctions(factory.factorySystem, factory.gameData.mainPlayer, assemblerId);
                }
            }

            return;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(BuildTool_Click), "CheckBuildConditions")]
        public static void CheckBuildConditionsPatch(BuildTool_Click __instance, ref bool __result)
        {
            if (__instance.buildPreviews.Count == 0)
            {
                return;
            }

            GameHistoryData history = __instance.actionBuild.history;

            bool isNoLevelLimit = true;
            for (int i = 0; i < __instance.buildPreviews.Count; i++)
            {
                BuildPreview buildPreview = __instance.buildPreviews[i];
                if (buildPreview.condition != 0)
                {
                    continue;
                }

                if (buildPreview.desc.isAssembler && buildPreview.desc.multiLevel)
                {
                    int id = buildPreview.item.ID;

                    ModelSetting setting;
                    if (ModelSettingDict.TryGetValue(id, out setting))
                    {
                        var storageResearchLevel = history.storageLevel - 2;
                        if (storageResearchLevel < setting.multiLevelMaxBuildCount.Length) // 为防万一，如果垂直建设研究的最大等级超过本MOD开发时设定的最大等级6，则不执行任何操作
                        {
                            int level = setting.multiLevelMaxBuildCount[storageResearchLevel];
                            int maxCount = setting.multiLevelMaxBuildCount[6];

                            int verticalCount = 0;
                            if (buildPreview.inputObjId != 0)
                            {
                                __instance.factory.ReadObjectConn(buildPreview.inputObjId, PlanetFactory.kMultiLevelInputSlot, out var isOutput, out var otherObjId, out var otherSlot);
                                while (otherObjId != 0)
                                {
                                    verticalCount++;
                                    __instance.factory.ReadObjectConn(otherObjId, PlanetFactory.kMultiLevelInputSlot, out isOutput, out otherObjId, out otherSlot);
                                }
                            }

                            if (level >= 2 && verticalCount >= level - 1)
                            {
                                isNoLevelLimit = level >= maxCount;
                                buildPreview.condition = EBuildCondition.OutOfVerticalConstructionHeight;
                                continue;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < __instance.buildPreviews.Count; i++)
            {
                BuildPreview buildPreview3 = __instance.buildPreviews[i];
                if (buildPreview3.condition == EBuildCondition.OutOfVerticalConstructionHeight)
                {
                    __instance.actionBuild.model.cursorState = -1;
                    __instance.actionBuild.model.cursorText = buildPreview3.conditionText;
                    if (!isNoLevelLimit)
                    {
                        __instance.actionBuild.model.cursorText += "垂直建造可升级".Translate();
                    }

                    if (!VFInput.onGUI)
                    {
                        UICursor.SetCursor(ECursor.Ban);
                    }

                    __result = false;

                    break;
                }
            }
        }
    }
}