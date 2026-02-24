# AssemblerVerticalConstruction

允许制造台、熔炉、化工厂等生产建筑像研究站一样进行垂直建造。

Originally released by FYJ95 [here](https://dsp.thunderstore.io/package/57a103a40a4d4d7f/AssemblerVerticalConstruction/).

組立機、製錬所、化学プラントなどのアセンブラがマトリックスラボのように垂直に建設できるようになります。

## 更新日志

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
