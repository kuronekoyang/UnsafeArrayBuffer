# UnsafeArrayBuffer
High performance zero allocation array buffer (but unsafe)

[UnsafeStringBuffer](https://github.com/kuronekoyang/UnsafeStringBuffer)姊妹篇

当你的项目中存在一些古代插件，它的API只能接受Array参数时，你可以用UnsafeArrayBuffer来优化数组的Allocation。

## 实现原理

UnsafeArrayBuffer内部使用[System.Buffers.ArrayPool](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1)数组对象池，可以高效利用内存。

再通过[UnsafeUtility.As](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Unity.Collections.LowLevel.Unsafe.UnsafeUtility.As.html)，修改Array的Count字段，让Array成为了变长数组

## 使用方法

```csharp
using var buffer = new UnsafeArrayBuffer<Vector3>();
buffer.Add(Vector3.right);
buffer.Add(Vector3.left);
buffer.Insert(1, Vector3.up);
buffer.Add(Vector3.one, 3);
buffer.RemoveAt(2);
var mesh = new Mesh();
mesh.SetVertices(buffer.InternalBuffer); // Zero Allocation
```

## 注意事项

InternalBuffer只能使用在临时场景。
