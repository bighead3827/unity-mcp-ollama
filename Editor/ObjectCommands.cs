using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using Newtonsoft.Json.Linq;

/// <summary>
    /// 实现Unity MCP仓库中缺失的对象相关命令
    /// </summary>
public static class ObjectCommands
{
    /// <summary>
    /// 根据名称搜索对象
    /// </summary>
    /// <param name="name">要搜索的名称</param>
    /// <returns>找到的对象列表</returns>
    public static GameObject[] FindObjectsByName(string name)
    {
        // 搜索场景内的所有GameObject
        var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>();
        
        // 如果未指定名称，则返回所有对象
        if (string.IsNullOrEmpty(name))
            return allObjects;
        
        // 过滤名称匹配或包含指定名称的对象
        return allObjects.Where(obj => obj.name.Contains(name)).ToArray();
    }

    /// <summary>
    /// 设置对象的位置
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    /// <param name="position">新的位置 (Vector3)</param>
    public static void SetPosition(GameObject targetObject, Vector3 position)
    {
        if (targetObject != null)
        {
            targetObject.transform.position = position;
        }
    }

    /// <summary>
    /// 设置对象的旋转
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    /// <param name="rotation">新的旋转 (欧拉角)</param>
    public static void SetRotation(GameObject targetObject, Vector3 rotation)
    {
        if (targetObject != null)
        {
            targetObject.transform.eulerAngles = rotation;
        }
    }

    /// <summary>
    /// 设置对象的缩放
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    /// <param name="scale">新的缩放</param>
    public static void SetScale(GameObject targetObject, Vector3 scale)
    {
        if (targetObject != null)
        {
            targetObject.transform.localScale = scale;
        }
    }

    /// <summary>
    /// 设置对象的变换（位置、旋转、缩放）
    /// </summary>
    /// <param name="targetObject">目标对象</param>
    /// <param name="position">新的位置（可选）</param>
    /// <param name="rotation">新的旋转（可选）</param>
    /// <param name="scale">新的缩放（可选）</param>
    public static void SetTransform(GameObject targetObject, Vector3? position = null, Vector3? rotation = null, Vector3? scale = null)
    {
        if (targetObject == null)
        {
            Debug.LogWarning("Cannot set transform for null object");
            return;
        }
        
        // パラメータが一つも指定されていない場合は警告
        if (!position.HasValue && !rotation.HasValue && !scale.HasValue)
        {
            Debug.LogWarning($"No transform parameters specified for object '{targetObject.name}'");
            return;
        }
        
        if (position.HasValue)
        {
            targetObject.transform.position = position.Value;
            Debug.Log($"Set position of '{targetObject.name}' to {position.Value}");
        }
        
        if (rotation.HasValue)
        {
            targetObject.transform.eulerAngles = rotation.Value;
            Debug.Log($"Set rotation of '{targetObject.name}' to {rotation.Value}");
        }
        
        if (scale.HasValue)
        {
            targetObject.transform.localScale = scale.Value;
            Debug.Log($"Set scale of '{targetObject.name}' to {scale.Value}");
        }
    }

    /// <summary>
    /// 从Vector3数组创建Vector3的辅助方法
    /// </summary>
    /// <param name="values">值的数组 [x, y, z]</param>
    /// <returns>Vector3</returns>
    public static Vector3 ParseVector3(float[] values)
    {
        if (values == null || values.Length < 3)
        {
            Debug.LogWarning("Invalid Vector3 array values, using Vector3.zero instead");
            return Vector3.zero;
        }
        
        return new Vector3(values[0], values[1], values[2]);
    }

    /// <summary>
    /// 从JArray创建Vector3的辅助方法
    /// </summary>
    /// <param name="jArray">JArray格式的值</param>
    /// <returns>Vector3</returns>
    public static Vector3 ParseVector3(JArray jArray)
    {
        if (jArray == null || jArray.Count < 3)
        {
            Debug.LogWarning("Invalid JArray values for Vector3, using Vector3.zero instead");
            return Vector3.zero;
        }
        
        try
        {
            return new Vector3(
                jArray[0].Value<float>(),
                jArray[1].Value<float>(),
                jArray[2].Value<float>()
            );
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error parsing Vector3 from JArray: {ex.Message}");
            return Vector3.zero;
        }
    }
}
