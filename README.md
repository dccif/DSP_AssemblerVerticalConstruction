# AssemblerVerticalConstruction

允许制造台、熔炉、化工厂等生产建筑像研究站一样进行垂直建造。

Originally released by FYJ95 [here](https://dsp.thunderstore.io/package/57a103a40a4d4d7f/AssemblerVerticalConstruction/).

組立機、製錬所、化学プラントなどのアセンブラがマトリックスラボのように垂直に建設できるようになります。

## 更新日志

### v1.1.11
- 修复多线程环境下拆除堆叠装配器时可能出现的 `ArgumentNullException` 崩溃问题。当装配器在并行 tick 处理期间被移除时，entityId 变为 0 导致互斥锁对象为 null，现已添加防御性检查。

### v1.1.10
- 修复堆叠制造台之间物品传输的吞吐量瓶颈。将原本每 tick 固定移动 `productCount * 2` 个产物的上限，修改为根据下方制造台的剩余产物缓存空间进行传输，解决高速或高层堆叠时上方产物积压、下方无法及时排空的问题。

### v1.1.9
- 兼容 ProjectGenesis（创世之书）mod，支持等离子熔炉进行垂直建造

### v1.1.8
- 修复与 SampleAndHoldSim 等 mod 同时使用时的兼容性问题（DivideByZeroException）
- 增加对装配器数据异常状态的防御性检查，提升整体稳定性

### v1.1.7
- 修复生产逻辑，修复增产剂效果不传递以及物品数量不正确的 bug
- 同步增产/加速模式（forceAccMode）到所有堆叠的装配器以及蓝图

### v1.1.6
- 适配最新 v0.10.34.28281
- 垂直建造以及蓝图时增产剂可以同步
- 修复多线程版本

---

## Description

Allows assemblers, smelters, chemical plants, and other production buildings to be constructed vertically, similar to Matrix Labs.

## Changelog

### v1.1.11
- Fixed an `ArgumentNullException` crash that could occur when removing stacked assemblers in a multi-threaded environment. When an assembler is removed during parallel tick processing, its entityId becomes 0, causing the mutex object to be null. Added a defensive check to prevent this.

### v1.1.10
- Fixed a throughput bottleneck in output transfer between stacked assemblers. Changed the original fixed transfer limit of `productCount * 2` items per tick to transfer up to the remaining buffer capacity of the lower assembler. This resolves the issue where upper layers keep products buffered on high-speed or tall stacked machines while lower layers cannot drain them fast enough.

### v1.1.9
- Added compatibility with the ProjectGenesis mod: Plasma Furnace now supports vertical construction

### v1.1.8
- Fixed compatibility issue (DivideByZeroException) when used with mods like SampleAndHoldSim
- Added defensive checks for abnormal assembler data states, improving overall stability

### v1.1.7
- Fixed production logic, including proliferator info transfer and incorrect item count bugs
- Synchronized Productivity/Acceleration mode (forceAccMode) for stacked assemblers and blueprints

### v1.1.6
- Compatible with v0.10.34.28281
- Proliferator settings now sync during vertical construction and blueprinting
- Fixed issues in the multi-threaded version
