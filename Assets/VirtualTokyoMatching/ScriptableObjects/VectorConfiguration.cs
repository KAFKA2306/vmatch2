using UnityEngine;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// ScriptableObject containing the transformation matrices and configurations
    /// for vector calculations and dimensionality reduction.
    /// </summary>
    [CreateAssetMenu(fileName = "VectorConfiguration", menuName = "VTM/Vector Configuration")]
    public class VectorConfiguration : ScriptableObject
    {
        [Header("112 -> 30D Transformation Matrix (W)")]
        [SerializeField] private float[] weightMatrix = new float[112 * 30]; // 112 questions -> 30 dimensions

        [Header("30D -> 6D Projection Matrix (P)")]
        [SerializeField] private float[] projectionMatrix = new float[30 * 6]; // 30D -> 6D reduction

        [Header("Axis Labeling")]
        [SerializeField] private string[] axisNames = new string[30];
        [SerializeField] private string[] reducedAxisNames = new string[6];

        [Header("Normalization Parameters")]
        [Range(0.001f, 1f)]
        public float epsilon = 0.001f; // For numerical stability in normalization

        /// <summary>
        /// Get the weight for transforming question response to 30D vector
        /// </summary>
        /// <param name="questionIndex">Question index (0-111)</param>
        /// <param name="axisIndex">Target axis index (0-29)</param>
        /// <returns>Weight value</returns>
        public float GetWeight(int questionIndex, int axisIndex)
        {
            if (questionIndex < 0 || questionIndex >= 112 || axisIndex < 0 || axisIndex >= 30)
                return 0f;

            return weightMatrix[questionIndex * 30 + axisIndex];
        }

        /// <summary>
        /// Set the weight for transforming question response to 30D vector
        /// </summary>
        public void SetWeight(int questionIndex, int axisIndex, float weight)
        {
            if (questionIndex < 0 || questionIndex >= 112 || axisIndex < 0 || axisIndex >= 30)
                return;

            weightMatrix[questionIndex * 30 + axisIndex] = weight;
        }

        /// <summary>
        /// Get the projection weight for reducing 30D to 6D
        /// </summary>
        /// <param name="axis30Index">30D axis index (0-29)</param>
        /// <param name="axis6Index">6D axis index (0-5)</param>
        /// <returns>Projection weight</returns>
        public float GetProjectionWeight(int axis30Index, int axis6Index)
        {
            if (axis30Index < 0 || axis30Index >= 30 || axis6Index < 0 || axis6Index >= 6)
                return 0f;

            return projectionMatrix[axis30Index * 6 + axis6Index];
        }

        /// <summary>
        /// Set the projection weight for reducing 30D to 6D
        /// </summary>
        public void SetProjectionWeight(int axis30Index, int axis6Index, float weight)
        {
            if (axis30Index < 0 || axis30Index >= 30 || axis6Index < 0 || axis6Index >= 6)
                return;

            projectionMatrix[axis30Index * 6 + axis6Index] = weight;
        }

        /// <summary>
        /// Get axis name for 30D vector
        /// </summary>
        public string GetAxisName(int axisIndex)
        {
            if (axisIndex < 0 || axisIndex >= 30 || axisNames == null)
                return $"Axis_{axisIndex}";

            return string.IsNullOrEmpty(axisNames[axisIndex]) ? $"Axis_{axisIndex}" : axisNames[axisIndex];
        }

        /// <summary>
        /// Get axis name for 6D reduced vector
        /// </summary>
        public string GetReducedAxisName(int axisIndex)
        {
            if (axisIndex < 0 || axisIndex >= 6 || reducedAxisNames == null)
                return $"Reduced_{axisIndex}";

            return string.IsNullOrEmpty(reducedAxisNames[axisIndex]) ? $"Reduced_{axisIndex}" : reducedAxisNames[axisIndex];
        }

        private void OnValidate()
        {
            // Ensure arrays are properly sized
            if (weightMatrix == null || weightMatrix.Length != 112 * 30)
            {
                weightMatrix = new float[112 * 30];
            }

            if (projectionMatrix == null || projectionMatrix.Length != 30 * 6)
            {
                projectionMatrix = new float[30 * 6];
            }

            if (axisNames == null || axisNames.Length != 30)
            {
                axisNames = new string[30];
                for (int i = 0; i < 30; i++)
                {
                    if (string.IsNullOrEmpty(axisNames[i]))
                        axisNames[i] = $"Axis_{i}";
                }
            }

            if (reducedAxisNames == null || reducedAxisNames.Length != 6)
            {
                reducedAxisNames = new string[6];
                for (int i = 0; i < 6; i++)
                {
                    if (string.IsNullOrEmpty(reducedAxisNames[i]))
                        reducedAxisNames[i] = $"Reduced_{i}";
                }
            }
        }
    }
}