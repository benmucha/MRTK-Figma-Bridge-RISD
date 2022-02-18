using UnityEngine;


namespace Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter
{
    public class DebugComponent : MonoBehaviour
    {
        private NodeData node;
        public string nodeName;
        public string nodeType;
        public string rawBoundingBox;
        public string scaledBoundingBox;
        public string containingFrame;
        public string debug;

        public void Init(NodeData node, float positionScale, Frame containingFrame)
        {
            this.node = node;
            this.nodeName = node.name;
            this.nodeType = node.type.ToString();
            this.containingFrame = containingFrame?.gameObject.name;
            if (node.absoluteBoundingBox != null)
            {
                this.rawBoundingBox = node.absoluteBoundingBox.ToString();
                Vector3 scaledPos = (node.absoluteBoundingBox.Position * positionScale);
                this.scaledBoundingBox = GetVectorString(scaledPos) + $" {node.absoluteBoundingBox.width * positionScale}x{node.absoluteBoundingBox.height * positionScale}";
            }
            else
            {
                this.rawBoundingBox = "NULL";
                this.scaledBoundingBox = "NULL";
            }
        }

        private static string GetVectorString(Vector3 vector)
        {
            return "(" + vector.x + ", " + vector.y + ")";
        }
    }
}