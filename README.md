# Lua Type Hint Generator (Unity)



本工具用于在Unity环境下为C#类型生成Lua注解文件, 以便提供类型检查和自动提示功能.

适用于安装了**EmmyLua**或**SumnekoLua**(VSCode中称为Lua)插件的IDE, 其余类型注解格式相似的插件或许也能使用.



## 参考

本项目自以下工程fork而来, 我在此基础上增加了一些实用功能并修复了些许Bug.

[ak47007tiger/EmmyLuaXLuaSnippetGenerator: generate xlua snippet for emmylua](https://github.com/ak47007tiger/EmmyLuaXLuaSnippetGenerator)

赞颂原作者的开源精神!

### 新功能

- [支持推理泛型字段类型的功能.](#泛型)
- [支持不带CS.前缀alias的生成.](#前缀)
- [支持将类型注解文件生成为多个, 提升编辑器中类型推断的性能.](#性能)
- [支持全局变量生成](#全局)和xLua的typeof函数生成.
- [编辑器内的简易GUI工具](#设置), 允许将生成注解的选项保存为本地配置文件, 免去需要修改源代码的麻烦.

### 问题修复

- 修复部分静态函数仍会生成self参数的问题.

- 修复泛型类型信息尾缀清除错误的问题.

- 不再生成匿名类型的注解.

  

## 使用

克隆本工程到你的项目目录下. 在Unity编辑器顶部的"LuaType"菜单中使用它. 请先 [设置] 再 [生成注解文件] .

<h1 id="设置"></h1>

## 设置

在生成注解文件前必须进行设置. 你可以根据以下说明进行设置, 设置的配置文件将保存在Unity编辑器的安装目录下(`AppDomain.CurrentDomain.BaseDirectory`), 不会存储在你的项目中.

<h1 id="路径"></h1>

### 生成类型注解文件的路径

提供一个仅包含目录名的绝对路径, 以反斜线\结尾. 

如果生成的注解较多, 可能会生成多个注解文件. 最好提供一个全新的空目录方便管理.

如果你在使用SumnekoLua, 强烈建议将本目录设置在项目外, 并在SumnekoLua的workspace.library设置中将本目录添加为库文件. 这不仅能防止注解文件对版本管理产生干扰, 也能提升类型分析的性能.

<h1 id="控件"></h1>

### 要生成注解的C#命名空间

默认会通过 `AppDomain.CurrentDomain.GetAssemblies()` 来收集当前域下的命名空间. 如果你使用了其它库(如DOTween), 填写更多命名空间并用空格分隔即可. 如:

`DG FairyGUI`

<h1 id="全局"></h1>

### 要生成注解的全局变量

项目中的部分全局变量可能从CSharp端设置, Lua无法识别它们. 在本配置中设置你项目中用到的全局变量及类型, 防止Lua频繁提示未定义字段的warning. 格式为`变量名:类型名`, 多个组用空格分隔. 如:

`UNITY_EDITOR:boolean, DEBUG_LEVEL:number`

请确保这些注释不会影响变量原本的值. 如果注解文件不能和项目解耦, 则不建议开启这个选项.

<h1 id="前缀"></h1>

### 生成不带CS.前缀的alias

本工具生成的类型提示会默认带有CS.前缀 (如`CS.UnityEngine.Vector3`), 可能是由于xLua访问C#类型时需要添加它.

如果你不需要这个前缀或项目中已经大量编写了不带CS.前缀的注释, 可以启用本选项以获得兼容.

```lua
---@type CS.UnityEngine.Vector3
local vec1 = CS.UnityEngine.Vector3.zero

---@type UnityEngine.Vector3
local vec2 = CS.UnityEngine.Vector3.zero

-- 启用该选项后, 是否带CS.前缀的两个版本将被识别为同一个类型, 即该赋值语句不会产生warning.
vec1 = vec2 

-- 注意! 这只适用于类型提示文件, 如果你使用xLua, 你仍然需要添加CS.前缀去在代码中真正访问C#类型.
-- 以下语句将在运行时报错, 即使你启用了本选项.
local vec3 = UnityEngine.Vector3.zero
```

<h1 id="泛型"></h1>

### 尝试推理泛型字段类型

开启后, 将会尝试推理泛型类的非泛型派生中, 继承而来的泛型字段/属性的实际类型. 参考以下代码:

```c#
class Singleton<T> {
    public T Instance { get; }
    public T instance;
}

class SomeManager : Singleton<SomeManager> {
    public void DoSomething() {}
}

// 对于泛型类Singleton, 程序将会生成如下格式的注解:
---@class Singleton
---@field Instance CS.T
---@field instance CS.T -- 未知类型的泛型参数直接生成为CS.参数名
local Singleton = {}

// 若开启本选项, 生成SomeManager类的注解时将能够分析Instance和instance的实际类型为SomeManager, 并在生成时添加它们的具体类型注释, 覆盖父类Singleton中的类型定义.
---@class SomeManager
---@field Instance SomeManager -- infer from Singleton`1[SomeManager]
---@field instance SomeManager -- infer from Singleton`1[SomeManager]
local SomeManager = {}

-- 运行环境:
-- 可以提示出DoSomething方法, 因为Instance类型已经被识别为SomeManager
someManager.Instance.DoSomething()

// 否则, 该类型将沿用Singleton中的泛型类型CS.T, 不会在SomeManager中生成覆盖类型的字段.
---@class SomeManager
local SomeManager = {}

-- 运行环境:
-- 打出Instance后不再能自动提示后续字段, 因为Instance类型未被推理, 而是被识别为CS.T
someManager.Instance.DoSomething() -- cause warning: undefied field "DoSomething"

```

如果你的项目中大量使用了泛型, 强烈推荐开启本功能, 因为调用链上的某个字段丢失类型信息将影响后续所有类型. 开启本选项将显著影响生成注解文件的速度, 但**不会**影响编辑器分析类型的速度. 考虑到注解文件不会频繁地被生成, 这是一笔划算的开销.

<h1 id="性能"></h1>

### 单个注解文件的最大行数

项目较大时, 生成的类型注解文件可能非常庞大, 甚至高达数十万行. 为了避免大文件带来的卡顿问题, 程序将按你设定的行数将文件拆分为多个页.

你可以根据自己的电脑配置设定这个值. 在我的i7-11700 + 64GB RAM的Windows台式电脑上, 设定为20000能发挥较好的性能.

> 如果你使用的是SumnekoLua, 合理设置本选项将大幅度提升编辑器分析类型的性能, 它的LuaLS在处理多文件时性能似乎比单文件更好. 在我的实际使用中, 这种感知也相当明显.参见:  [[Question\] Large size type annotation file cause low performance. Is it possible to fix it? · Issue #2674 · LuaLS/lua-language-server](https://github.com/LuaLS/lua-language-server/issues/2674) 
