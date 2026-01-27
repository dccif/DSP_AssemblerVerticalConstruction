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
                            return;
                        }
                    }
                }
            }
            while (objId != 0);
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
                    int transfar = Math.Min(served, nextNeeds);

                    if (incServed <= 0)
                    {
                        incServed = 0;
                    }

                    //var args = new object[] { _this.served[i], incServed, transfar };
                    //int out_one_inc_level = Traverse.Create(nextAssembler).Method("split_inc_level", new System.Type[] { typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(int) }).GetValue<int>(args);
                    //_this.served[i] = (int)args[0];
                    //incServed = (int)args[1];

                    // 备注：实际上，正确做法应该是调用 nextAssembler.split_inc_level()
                    // 但 split_inc_level() 本应声明为 static，却未被设为 static，而且还被设为 private
                    // 导致从这里调用它不可避免地会产生额外开销
                    // 因此，决定直接将 split_inc_level() 的实现代码复制过来使用
                    int out_one_inc_level = split_inc_level(ref _this.served[i], ref incServed, transfar);
                    if (_this.served[i] == 0)
                    {
                        incServed = 0;
                    }

                    nextAssembler.served[i] += transfar;
                    // 保持 incServed / served 比例一致
                    if (nextAssembler.served[i] > 0)
                    {
                        long newInc = (long)nextAssembler.incServed[i] * (nextAssembler.served[i] + transfar) / nextAssembler.served[i];
                        nextAssembler.incServed[i] = (int)Math.Min(newInc, int.MaxValue);
                    }
                    else
                    {
                        // 初始状态，按来源比例
                        nextAssembler.incServed[i] = (int)((long)incServed * transfar / Math.Max(1, _this.served[i] + transfar));
                    }
                    nextAssembler.served[i] += transfar;
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
        private static int split_inc_level(ref int n, ref int m, int p)
        {
            int num = m / n;
            int num2 = m - num * n;
            n -= p;
            num2 -= n;
            m -= ((num2 > 0) ? (num * p + num2) : (num * p));
            return num;
        }
    }
}