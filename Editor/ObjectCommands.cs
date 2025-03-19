using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using Newtonsoft.Json.Linq;

/// <summary>
/// Unity MCPリポジトリで不足しているオブジェクト関連コマンドの実装
/// </summary>
public static class ObjectCommands
{
    /// <summary>
    /// 名前に基づいてオブジェクトを検索する
    /// </summary>
    /// <param name="name">検索する名前</param>
    /// <returns>見つかったオブジェクトのリスト</returns>
    public static GameObject[] FindObjectsByName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return new GameObject[0];

        // シーン内の全GameObjectを検索
        var allObjects = UnityEngine.Object.FindObjectsOfType<GameObject>(true);
        
        // 名前が一致または含まれるオブジェクトをフィルタリング
        return allObjects.Where(obj => obj.name.Contains(name)).ToArray();
    }

    /// <summary>
    /// オブジェクトの位置を設定する
    /// </summary>
    /// <param name="targetObject">対象オブジェクト</param>
    /// <param name="position">新しい位置 (Vector3)</param>
    public static void SetPosition(GameObject targetObject, Vector3 position)
    {
        if (targetObject != null)
        {
            targetObject.transform.position = position;
        }
    }

    /// <summary>
    /// オブジェクトの回転を設定する
    /// </summary>
    /// <param name="targetObject">対象オブジェクト</param>
    /// <param name="rotation">新しい回転 (Euler angles)</param>
    public static void SetRotation(GameObject targetObject, Vector3 rotation)
    {
        if (targetObject != null)
        {
            targetObject.transform.eulerAngles = rotation;
        }
    }

    /// <summary>
    /// オブジェクトのスケールを設定する
    /// </summary>
    /// <param name="targetObject">対象オブジェクト</param>
    /// <param name="scale">新しいスケール</param>
    public static void SetScale(GameObject targetObject, Vector3 scale)
    {
        if (targetObject != null)
        {
            targetObject.transform.localScale = scale;
        }
    }

    /// <summary>
    /// オブジェクトのトランスフォーム（位置、回転、スケール）を設定する
    /// </summary>
    /// <param name="targetObject">対象オブジェクト</param>
    /// <param name="position">新しい位置（オプション）</param>
    /// <param name="rotation">新しい回転（オプション）</param>
    /// <param name="scale">新しいスケール（オプション）</param>
    public static void SetTransform(GameObject targetObject, Vector3? position = null, Vector3? rotation = null, Vector3? scale = null)
    {
        if (targetObject == null)
            return;
        
        if (position.HasValue)
            targetObject.transform.position = position.Value;
        
        if (rotation.HasValue)
            targetObject.transform.eulerAngles = rotation.Value;
        
        if (scale.HasValue)
            targetObject.transform.localScale = scale.Value;
    }

    /// <summary>
    /// Vector3配列からVector3を作成するヘルパーメソッド
    /// </summary>
    /// <param name="values">値の配列 [x, y, z]</param>
    /// <returns>Vector3</returns>
    public static Vector3 ParseVector3(float[] values)
    {
        if (values == null || values.Length < 3)
            return Vector3.zero;
        
        return new Vector3(values[0], values[1], values[2]);
    }

    /// <summary>
    /// JArrayからVector3を作成するヘルパーメソッド
    /// </summary>
    /// <param name="jArray">JArray形式の値</param>
    /// <returns>Vector3</returns>
    public static Vector3 ParseVector3(JArray jArray)
    {
        if (jArray == null || jArray.Count < 3)
            return Vector3.zero;
        
        return new Vector3(
            jArray[0].Value<float>(),
            jArray[1].Value<float>(),
            jArray[2].Value<float>()
        );
    }
}
