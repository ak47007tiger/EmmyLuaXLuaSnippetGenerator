# Lua Type Hint Generator (Unity)



本工具用于在Unity环境下为C#类型生成Lua注解文件, 以便提供类型检查和自动提示功能.

适用于安装了**EmmyLua**或**SumnekoLua**(VSCode中称为Lua)插件的IDE, 其余类型注解格式相似的插件或许也能使用.



## 参考

本项目自以下工程fork而来, 我在此基础上增加了一些实用功能并修复了些许Bug.

[ak47007tiger/EmmyLuaXLuaSnippetGenerator: generate xlua snippet for emmylua](https://github.com/ak47007tiger/EmmyLuaXLuaSnippetGenerator)

赞颂原作者的开源精神!

### 新功能

- [支持推理泛型字段类型的功能.](#泛型)
- [支持CS.前缀alias的生成.](#前缀)
- [允许C#函数类型与Lua Function类型兼容.](#函数兼容)
- [支持将类型注解文件生成为多个, 提升编辑器中类型推断的性能.](#性能)
- [支持全局变量生成](#全局)和xLua的typeof函数生成.
- [编辑器内的简易GUI工具](#设置), 允许将生成注解的选项保存为本地配置文件, 免去需要修改源代码的麻烦.

### 问题修复

- 修复部分静态函数仍会生成self参数的问题.
- 修复泛型类型信息尾缀清除错误的问题.
- 不再生成匿名类型的注解.

  

## 使用

克隆本工程到你的项目目录下. 在Unity编辑器顶部的"LuaType"菜单中使用它. 请先 [设置] 再 [生成注解文件].

每次生成注解文件时, 程序会自动清空目标目录, 你也可以点击 [清除类型注解] 来手动清除它们.

<h1 id="设置"></h1>

## 设置

在生成注解文件前必须进行设置. 你可以根据以下说明进行设置.

<h1 id="配置路径"></h1>

### 配置文件的存放路径

提供一个仅包含目录名的绝对路径, 是否以斜线/结尾都可以.

该路径的默认值为`AppDomain.CurrentDomain.BaseDirectory`, 在Unity中通常在引擎的安装目录下. 如果对此默认目录的访问被拒绝, 请尝试重新设定该目录.

<h1 id="路径"></h1>

### 生成类型注解文件的路径

提供一个仅包含目录名的绝对路径, 必须以斜线/结尾.

如果生成的注解较多, 可能会生成多个注解文件. 最好提供一个全新的空目录方便管理. 通常来说, 注解文件的目录需要设置在项目内, 以便你的IDE分析它们.

但如果你在使用**SumnekoLua**, 我更建议你将本目录设置在项目外, 然后在**SumnekoLua**的`workspace.library`设置中添加这个目录. 这不仅能防止注解文件对版本管理产生干扰, 也能提升类型分析的性能. 具体操作方法可以查看 [Libraries · LuaLS/lua-language-server Wiki](https://github.com/LuaLS/lua-language-server/wiki/Libraries#link-to-workspace)

<h1 id="控件"></h1>

### 要生成注解的C#命名空间

默认会通过 `AppDomain.CurrentDomain.GetAssemblies()` 来收集当前域下的命名空间. 如果你使用了其它库(如DOTween), 填写更多命名空间并用空格分隔即可. 如:

`DG FairyGUI`

<h1 id="全局"></h1>

### 要生成注解的全局变量

项目中的部分全局变量可能从CSharp端设置, Lua无法识别它们. 在本配置中设置你项目中用到的全局变量及类型, 防止Lua频繁提示未定义字段的warning. 格式为`变量名:类型名`, 多个组用空格分隔. 如:

`UNITY_EDITOR:boolean DEBUG_LEVEL:number`

<h1 id="函数兼容"></h1>

### 使以下类型名兼容Lua function类型

C#中的一些函数类型(如`System.Action`)和Lua的`function`类型间没有默认的隐式转换. 尽管xLua能够处理它们, 但类型系统可能会提示warning:

```lua
local luaFunc = function() end

---@type System.Action
local csharpFunc

csharpFunc = luaFunc -- cause warning: cannot convert type "function" to "System.Action"
```

你可以开启本选项以允许类型与Lua function兼容. 在文本框中输入你想兼容Lua function的类型全名, 多个类型名用空格分隔:

`System.Action FairyGUI.EventCallback0`

程序将会以交联类型`t | function`的形式来处理你填写的类型名, 所以你不会因为这些兼容而丢失该类型原本的字段信息.

<h1 id="前缀"></h1>

### 生成带CS.前缀的兼容alias

本工具生成的类型通常仅包含类型名 (如`UnityEngine.Vector3`).

如果你的项目中使用了带有`CS.`前缀版本的类型注解, 如`CS.UnityEngine.Vector3`, 或者你希望能在访问xLua的`CS`表时获得自动提示, 可以启用本选项以获得兼容.

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

开启后, 将会尝试推理泛型类的非泛型派生中, 继承而来的泛型字段/属性的实际类型. 如以下C#代码:

```c#
class Singleton<T> {
    public T Instance { get; }
    public T instance;
}

class SomeManager : Singleton<SomeManager> {
    public void DoSomething() {}
}
```

在未开启本选项时, 这段代码的注解会按如下格式生成:

```lua
---@class Singleton
---@field Instance T
---@field instance T -- 未知类型的泛型参数直接生成为参数名
local Singleton = {}

---@class SomeManager
local SomeManager = {}

-- 编写业务代码时:
-- 打出Instance后不能自动提示后续字段, 因为Instance类型未被推理, 而是被识别为T
local sm = SomeManager()
sm.Instance.DoSomething() -- cause warning: undefied field "DoSomething"
```

开启本选项后, 生成SomeManager类的注解时将能够分析Instance和instance的实际类型为SomeManager, 并在生成时添加它们的具体类型注释, 覆盖父类Singleton中的类型定义.

```lua
---@class Singleton
---@field Instance T
---@field instance T
local Singleton = {}

---@class SomeManager
---@field Instance SomeManager -- infer from Singleton`1[SomeManager]
---@field instance SomeManager -- infer from Singleton`1[SomeManager]
local SomeManager = {}

-- 编写业务代码时:
-- 可以提示出DoSomething方法, 因为Instance类型已经被识别为SomeManager
local sm = SomeManager()
sm.Instance.DoSomething()
```

理论上, 只要派生类的泛型参数数量少于父类, 我们就能通过这个原理获得部分类型信息. 受限于时间原因, 我暂时只做了**字段**和**属性**的推理以应对大部分场景, 非泛型方法的参数和返回值类型推断应该可以用类似的办法实现.

如果你的项目中大量使用了泛型, 强烈推荐开启本功能, 因为调用链上的某个字段丢失类型信息将影响后续所有类型. 开启本选项将减慢生成注解文件的速度, 但**不会**影响编辑器分析类型的速度. 考虑到注解文件不会频繁地被生成, 这是一笔划算的开销.

<h1 id="性能"></h1>

### 单个注解文件的最大行数

项目较大时, 生成的类型注解文件可能非常庞大, 甚至高达数十万行. 为了避免大文件带来的卡顿问题, 程序将按你设定的行数将文件拆分为多个页.

你可以根据自己的电脑配置设定这个值. 在我的i7-11700 + 64GB RAM的Windows台式电脑上, 设定为20000能发挥较好的性能.

> 如果你使用的是SumnekoLua, 合理设置本选项将大幅度提升编辑器分析类型的性能, 它的LuaLS在处理多文件时性能似乎比单文件更好. 在我的实际使用中, 这种感知也相当明显.参见:  [[Question\] Large size type annotation file cause low performance. Is it possible to fix it? · Issue #2674 · LuaLS/lua-language-server](https://github.com/LuaLS/lua-language-server/issues/2674) 
