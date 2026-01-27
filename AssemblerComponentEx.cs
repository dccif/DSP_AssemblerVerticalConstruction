using System;

namespace AssemblerVerticalConstruction
{
    public class AssemblerComponentEx
    {
        public int[][] assemblerNextIds = new int[64 * 6][]; // 位于上方的为 Next
        public int assemblerCapacity = 64 * 6;
        public int transfarCount = 6;// 每个游戏帧（tick）可传递多少个产出物品

        public void SetAssemblerCapacity(int newCapacity)
        {
            var oldAssemblerNextIds = this.assemblerNextIds;

            this.assemblerNextIds = new int[newCapacity][];

            if (oldAssemblerNextIds != null)
            {
                Array.Copy(oldAssemblerNextIds, this.assemblerNextIds, (newCapacity <= this.assemblerCapacity) ? newCapacity : assemblerCapacity);
            }

            this.assemblerCapacity = newCapacity;
        }

        public int GetNextId(int index, int assemblerId)
        {
            if (index >= assemblerNextIds.Length)
            {
                return 0;
            }

            if (this.assemblerNextIds[index] == null || assemblerId >= this.assemblerNextIds[index].Length)
            {
                return 0;
            }

            return this.assemblerNextIds[index][assemblerId];
        }

        public static void FindRecipeIdForBuild(FactorySystem factorySystem, int assemblerId)
        {
            // 从上下方的装配器获取并设置配方
            // 实现参考了 LabComponent.FindLabFunctionsForBuild()
            // 先从自身向下遍历
            var _this = factorySystem;
            int entityId = _this.assemblerPool[assemblerId].entityId;
            if (entityId == 0)
            {
                return;
            }

            bool isOutput;
            int otherObjId;
            int otherSlot;

            // 首先从自身开始向下逐级遍历。
            int objId = entityId;
            do
            {

                _this.factory.ReadObjectConn(objId, PlanetFactory.kMultiLevelInputSlot, out isOutput, out otherObjId, out otherSlot);
                objId = otherObjId;
                if (objId > 0)
                {
                    int assemblerId2 = _this.factory.entityPool[objId].assemblerId;
                    if (assemblerId2 > 0 && _this.assemblerPool[assemblerId2].id == assemblerId2)
                    {
                        if (_this.assemblerPool[assemblerId2].recipeId > 0)
                        {
                            _this.assemblerPool[assemblerId].SetRecipe(_this.assemblerPool[assemblerId2].recipeId, _this.factory.entitySignPool);
                            // 同步 forceAccMode
                            SyncForceAccModeFromSource(_this, assemblerId, assemblerId2);
                            return;
                        }
                    }
                }
            }
            while (objId != 0);

            // 如果不行，就从自身向上逐级回溯。
            objId = entityId;
            do
            {
                _this.factory.ReadObjectConn(objId, PlanetFactory.kMultiLevelOutputSlot, out isOutput, out otherObjId, out otherSlot);
                objId = otherObjId;
                if (objId > 0)
                {
                    int assemblerId3 = _this.factory.entityPool[objId].assemblerId;
                    if (assemblerId3 > 0 && _this.assemblerPool[assemblerId3].id == assemblerId3)
                    {
                        if (_this.assemblerPool[assemblerId3].recipeId > 0)
                        {
                            _this.assemblerPool[assemblerId].SetRecipe(_this.assemblerPool[assemblerId3].recipeId, _this.factory.entitySignPool);
                            // 同步 forceAccMode
                            SyncForceAccModeFromSource(_this, assemblerId, assemblerId3);
                            return;
                        }
                    }
                }
            }
            while (objId != 0);
        }

        // 从源装配器同步 forceAccMode 到目标装配器
        private static void SyncForceAccModeFromSource(FactorySystem factorySystem, int targetAssemblerId, int sourceAssemblerId)
        {
            ref var targetAssembler = ref factorySystem.assemblerPool[targetAssemblerId];
            ref var sourceAssembler = ref factorySystem.assemblerPool[sourceAssemblerId];

            // 仅当配方支持增产时才同步
            if (targetAssembler.recipeExecuteData != null && targetAssembler.recipeExecuteData.productive)
            {
                targetAssembler.forceAccMode = sourceAssembler.forceAccMode;
            }
        }

        public void SetAssemblerInsertTarget(PlanetFactory __instance, int assemblerId, int nextEntityId)
        {
            var index = __instance.factorySystem.factory.index;
            if (index >= assemblerNextIds.Length)
            {
                this.SetAssemblerCapacity(this.assemblerCapacity * 2);
            }

            if (assemblerId != 0 && __instance.factorySystem.assemblerPool[assemblerId].id == assemblerId)
            {
                if (nextEntityId == 0)
                {
                    this.assemblerNextIds[index][assemblerId] = 0;
                }
                else
                {
                    var nextAssemblerId = __instance.entityPool[nextEntityId].assemblerId;

                    this.assemblerNextIds[index][assemblerId] = nextAssemblerId;

                    // 同じレシピにする
                    FindRecipeIdForBuild(__instance.factorySystem, assemblerId);
                }
            }
        }

        public void UnsetAssemblerInsertTarget(PlanetFactory __instance, int assemblerId, int assemblerRemoveId)
        {
            var index = __instance.factorySystem.factory.index;
            if (assemblerId != 0 && __instance.factorySystem.assemblerPool[assemblerId].id == assemblerId)
            {
                this.assemblerNextIds[index][assemblerId] = 0;
            }
        }

        public void SetAssemblerNext(int index, int assemblerId, int nextId)
        {
            if (index >= assemblerNextIds.Length)
            {
                this.SetAssemblerCapacity(this.assemblerCapacity * 2);
            }

            if (assemblerNextIds[index] == null || assemblerId >= assemblerNextIds[index].Length)
            {
                var array = this.assemblerNextIds[index];

                var newCapacity = assemblerId * 2;
                newCapacity = newCapacity > 256 ? newCapacity : 256;
                this.assemblerNextIds[index] = new int[newCapacity];
                if (array != null)
                {
                    var len = array.Length;
                    Array.Copy(array, this.assemblerNextIds[index], (newCapacity <= len) ? newCapacity : len);
                }
            }

            this.assemblerNextIds[index][assemblerId] = nextId;
        }
        public void UpdateOutputToNext(PlanetFactory factory, int planeIndex, int assemblerId, AssemblerComponent[] assemblerPool, int assemblerNextId, bool useMutex)
        {
            if (useMutex)
            {
                var entityId = assemblerPool[assemblerId].entityId;
                var entityNextId = assemblerPool[assemblerNextId].entityId;

                lock (factory.entityMutexs[entityId])
                {
                    lock (factory.entityMutexs[entityNextId])
                    {
                        UpdateOutputToNextInner(assemblerId, assemblerNextId, assemblerPool);
                    }
                }
            }
            else
            {
                UpdateOutputToNextInner(assemblerId, assemblerNextId, assemblerPool);
            }
        }

        private void UpdateOutputToNextInner(int assemblerId, int assemblerNextId, AssemblerComponent[] assemblerPool)
        {
            ref var _this = ref assemblerPool[assemblerId];
            if (_this.served == null) // 备注：当配方为空时，served 会被设为 null
            {
                return;
            }

            ref var nextAssembler = ref assemblerPool[assemblerNextId];

            // 装配器用于存储原料的缓冲区的基本上限
            // 源自 AssemblerComponent.UpdateNeeds() 的代码
            int needsFactor = nextAssembler.speedOverride * 180 / nextAssembler.recipeExecuteData.timeSpend + 1;

            int servedLen = _this.served.Length;
            for (int i = 0; i < servedLen; i++)
            {
                int served = _this.served[i];
                int nextNeeds = nextAssembler.recipeExecuteData.requireCounts[i] * needsFactor - nextAssembler.served[i];
                if (nextNeeds > 0 && served > 0)
                {
                    ref int incServed = ref _this.incServed[i];

                    // 如果 assemblerId 中有原材料库存，则将其发送给 nextAssembler，以满足 nextNeeds 的需求。
                    int transfer = Math.Min(served, nextNeeds);

                    if (incServed <= 0)
                    {
                        incServed = 0;
                    }

                    // 使用 split_inc_level 从源装配器提取要传递的增产剂信息
                    // 返回值是每个物品的增产剂等级
                    int incLevel = split_inc_level(ref _this.served[i], ref incServed, transfer);
                    if (_this.served[i] == 0)
                    {
                        incServed = 0;
                    }

                    // 将物品和对应的增产剂信息传递给目标装配器
                    nextAssembler.served[i] += transfer;
                    // incServed = 物品数量 * 增产剂等级
                    nextAssembler.incServed[i] += transfer * incLevel;
                }
            }

            var productCountsLen = _this.recipeExecuteData.productCounts.Length;
            for (int l = 0; l < productCountsLen; l++)
            {
                var maxCount = _this.recipeExecuteData.productCounts[l] * 9;
                if (_this.produced[l] < maxCount && nextAssembler.produced[l] > 0)
                {
                    // 按比例传送原料
                    var count = Math.Min(_this.recipeExecuteData.productCounts[l] * 2, nextAssembler.produced[l]);
                    _this.produced[l] += count;
                    nextAssembler.produced[l] -= count;
                }
            }
        }


        // AssemblerComponent.split_inc_level() 为原版方法
        private int split_inc_level(ref int n, ref int m, int p)
        {
            int num1 = m / n;
            int num2 = m - num1 * n;
            n -= p;
            int num3 = num2 - n;
            m -= num3 > 0 ? num1 * p + num3 : num1 * p;
            return num1;
        }
    }
}