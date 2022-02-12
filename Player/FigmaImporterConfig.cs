using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Config FigmaImporter", menuName = "ScriptableObjects/FigmaImporterConfig", order = 1)]
public class FigmaImporterConfig : ScriptableObject
{
    public string FigmaFileID;
    public Vector2 frameSize;
}