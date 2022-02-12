using Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Frame : MonoBehaviour
{
    private Node node;
    public Vector3 absolutePos;

    public void Init(Node node)
    {
        this.node = node;
        this.absolutePos = node.absoluteBoundingBox.Position;
    }
}
