using UnityEngine;


namespace Microsoft.MixedReality.Toolkit.Utilities.FigmaImporter
{
    public class DebugComponent : MonoBehaviour
    {
        private Node node;
        public string name;
        public string rawBoundingBox;
        public string scaledBoundingBox;

        public void Init(Node node, float positionScale)
        {
            this.node = node;
            this.name = node.name;
            if (node.absoluteBoundingBox != null)
            {
                this.rawBoundingBox = node.absoluteBoundingBox.ToString();
                this.scaledBoundingBox = (node.absoluteBoundingBox.Position * positionScale).ToString();
            }
            else
            {
                this.rawBoundingBox = "NULL";
                this.scaledBoundingBox = "NULL";
            }
        }
    }
}