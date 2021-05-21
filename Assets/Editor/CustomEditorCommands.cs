using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

public class CustomEditorCommands
{
    [MenuItem("Custom Commands/Create Rule Tiles From Tilemap")]
    static void CreateRuleTile()
    {
        Debug.Log("Create Rule tile");
        //string path = EditorUtility.OpenFilePanel("Select rule tile layout file", Application.dataPath + "/Tilemaps/Rule Tiles/Rule Tile Schemas/", "json");

        // Load Rule tile
        string absRuleTilePath = EditorUtility.OpenFilePanel("Select rule tile to copy format", Application.dataPath + "/Tilemaps/Rule Tiles/", "asset");
        string localRuleTilePath = AbsToLocalPath(absRuleTilePath);
        RuleTile modelTile = (RuleTile)AssetDatabase.LoadAssetAtPath(localRuleTilePath, typeof(RuleTile));

        // Load Atlas
        string absAtlasPath = EditorUtility.OpenFilePanel("Select atlas to take sprites from", Application.dataPath + "/Textures/", "png");
        string localAtlasPath = AbsToLocalPath(absAtlasPath);
        Object[] atlasArray = AssetDatabase.LoadAllAssetsAtPath(localAtlasPath);//.OfType<Sprite>().ToArray();

        //RuleTile.TilingRule rule = new RuleTile.TilingRule();
        //rule.m_Output = RuleTile.TilingRule.OutputSprite.Single;
        //rule.m_Sprites[0] = tile.m_DefaultSprite;
        //rule.m_GameObject = tile.m_DefaultGameObject;
        //rule.m_ColliderType = tile.m_DefaultColliderType;
        //tile.m_TilingRules.Add(rule);

        RuleTile tile = ScriptableObject.CreateInstance<RuleTile>();
        tile.m_TilingRules = new List<RuleTile.TilingRule>();
        tile.m_DefaultColliderType = modelTile.m_DefaultColliderType;
        tile.m_DefaultGameObject = modelTile.m_DefaultGameObject;
        
        string defSprtNm = modelTile.m_DefaultSprite.name;
        int atlasIndx = int.Parse(defSprtNm.Substring(defSprtNm.LastIndexOf('_') + 1));
        tile.m_DefaultSprite = (Sprite)atlasArray[atlasIndx];


        for (int i = 0; i < modelTile.m_TilingRules.Count; i++)
        {
            RuleTile.TilingRule modelRule = modelTile.m_TilingRules[i];
            RuleTile.TilingRule rule = new RuleTile.TilingRule();
            
            // Copy variables from the mdoel rule into the new rule
            rule.m_Output = modelRule.m_Output;
            rule.m_GameObject = tile.m_DefaultGameObject;
            rule.m_ColliderType = tile.m_DefaultColliderType;
            
            // Get the index of the sprite in the atlas in the model rule
            string spriteName = modelRule.m_Sprites[0].name;
            int atlasIndex = int.Parse(spriteName.Substring(spriteName.LastIndexOf('_') + 1));
            
            // Get the correct sprite in the new atlas
            rule.m_Sprites[0] = (Sprite)atlasArray[atlasIndex];

            // Copy neighbor rules to new rule
            rule.ApplyNeighbors(modelRule.GetNeighbors());
            tile.m_TilingRules.Add(rule);
        }

        string savePath = EditorUtility.SaveFilePanelInProject("Save new rule tile", "NewRuleTile", "asset", "", "Assets/Tilemaps/Rule Tiles/");
        AssetDatabase.CreateAsset(tile, savePath);
    }

    static string AbsToLocalPath(string absolutePath)
    {
        string localPath = "Assets" + absolutePath.Substring(absolutePath.IndexOf(Application.dataPath) + Application.dataPath.Length, absolutePath.Length - (absolutePath.IndexOf(Application.dataPath) + Application.dataPath.Length));
        return localPath;
    }
    
}
