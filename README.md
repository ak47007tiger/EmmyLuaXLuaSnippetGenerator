# 使用
clone下来后放到unity工程，Generator.cs里修改TypeDefineFilePath导出类型标注代码到指定文件路径

# 原理说明
搜集所有当前运行的c#类型，过滤出需要导出的类型，使用反射生成对应的emmylua提示
代码都在Generator.cs

# 参考
最早是支持ToLua的类型标注生成，我加了修改支持了XLua，赞颂原作者的开源精神
https://github.com/LagField/EmmyLuaTypeGenerator
