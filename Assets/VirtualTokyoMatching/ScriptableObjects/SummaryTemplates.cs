using UnityEngine;
using System.Collections.Generic;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// ScriptableObject containing templates for auto-generating personality summaries
    /// and tags based on vector values.
    /// </summary>
    [CreateAssetMenu(fileName = "SummaryTemplates", menuName = "VTM/Summary Templates")]
    public class SummaryTemplates : ScriptableObject
    {
        [System.Serializable]
        public class AxisTemplate
        {
            [Header("Axis Information")]
            public string axisName;
            public int axisIndex; // 0-29 for 30D vector

            [Header("Positive Direction (>0.3)")]
            [TextArea(2, 3)]
            public string positiveDescription;
            public string[] positiveTags;

            [Header("Negative Direction (<-0.3)")]
            [TextArea(2, 3)]
            public string negativeDescription;
            public string[] negativeTags;

            [Header("Neutral Range (-0.3 to 0.3)")]
            [TextArea(2, 3)]
            public string neutralDescription;
            public string[] neutralTags;
        }

        [System.Serializable]
        public class HeadlineTemplate
        {
            public string template; // e.g., "{positive_trait}で{activity_preference}な人"
            public float minConfidence = 0.5f; // Minimum vector strength to use this template
        }

        [Header("Axis Templates (30 axes)")]
        [SerializeField] private AxisTemplate[] axisTemplates = new AxisTemplate[30];

        [Header("Headline Templates")]
        [SerializeField] private HeadlineTemplate[] headlineTemplates;

        [Header("Summary Configuration")]
        [Range(0.1f, 1f)]
        public float significanceThreshold = 0.3f; // Minimum |value| to be considered significant
        
        [Range(1, 5)]
        public int maxTagsPerProfile = 3;

        [Range(1, 3)]
        public int maxSentencesInSummary = 2;

        /// <summary>
        /// Get axis template by index
        /// </summary>
        public AxisTemplate GetAxisTemplate(int axisIndex)
        {
            if (axisIndex < 0 || axisIndex >= axisTemplates.Length)
                return null;

            return axisTemplates[axisIndex];
        }

        /// <summary>
        /// Generate summary text from 30D vector
        /// </summary>
        public string GenerateSummary(float[] vector30D)
        {
            if (vector30D == null || vector30D.Length != 30)
                return "未分析";

            var significantAxes = new List<(int index, float value, AxisTemplate template)>();

            // Find significant axes
            for (int i = 0; i < 30; i++)
            {
                if (Mathf.Abs(vector30D[i]) >= significanceThreshold && axisTemplates[i] != null)
                {
                    significantAxes.Add((i, vector30D[i], axisTemplates[i]));
                }
            }

            // Sort by absolute value (most significant first)
            significantAxes.Sort((a, b) => Mathf.Abs(b.value).CompareTo(Mathf.Abs(a.value)));

            // Generate summary sentences
            var sentences = new List<string>();
            int sentenceCount = 0;

            foreach (var (index, value, template) in significantAxes)
            {
                if (sentenceCount >= maxSentencesInSummary)
                    break;

                string description;
                if (value > significanceThreshold)
                    description = template.positiveDescription;
                else if (value < -significanceThreshold)
                    description = template.negativeDescription;
                else
                    description = template.neutralDescription;

                if (!string.IsNullOrEmpty(description))
                {
                    sentences.Add(description);
                    sentenceCount++;
                }
            }

            return sentences.Count > 0 ? string.Join("。", sentences) + "。" : "バランスの取れた価値観をお持ちです。";
        }

        /// <summary>
        /// Generate tags from 30D vector
        /// </summary>
        public string[] GenerateTags(float[] vector30D)
        {
            if (vector30D == null || vector30D.Length != 30)
                return new string[0];

            var tags = new List<string>();
            var significantAxes = new List<(int index, float value, AxisTemplate template)>();

            // Find significant axes
            for (int i = 0; i < 30; i++)
            {
                if (Mathf.Abs(vector30D[i]) >= significanceThreshold && axisTemplates[i] != null)
                {
                    significantAxes.Add((i, vector30D[i], axisTemplates[i]));
                }
            }

            // Sort by absolute value
            significantAxes.Sort((a, b) => Mathf.Abs(b.value).CompareTo(Mathf.Abs(a.value)));

            // Collect tags
            foreach (var (index, value, template) in significantAxes)
            {
                if (tags.Count >= maxTagsPerProfile)
                    break;

                string[] axisTags;
                if (value > significanceThreshold)
                    axisTags = template.positiveTags;
                else if (value < -significanceThreshold)
                    axisTags = template.negativeTags;
                else
                    axisTags = template.neutralTags;

                if (axisTags != null && axisTags.Length > 0)
                {
                    foreach (var tag in axisTags)
                    {
                        if (!string.IsNullOrEmpty(tag) && !tags.Contains(tag) && tags.Count < maxTagsPerProfile)
                        {
                            tags.Add(tag);
                        }
                    }
                }
            }

            return tags.ToArray();
        }

        /// <summary>
        /// Generate headline from 30D vector
        /// </summary>
        public string GenerateHeadline(float[] vector30D)
        {
            if (vector30D == null || vector30D.Length != 30 || headlineTemplates == null || headlineTemplates.Length == 0)
                return "新しいつながりを求めています";

            // Calculate confidence (average of significant values)
            float confidence = 0f;
            int significantCount = 0;

            for (int i = 0; i < 30; i++)
            {
                if (Mathf.Abs(vector30D[i]) >= significanceThreshold)
                {
                    confidence += Mathf.Abs(vector30D[i]);
                    significantCount++;
                }
            }

            confidence = significantCount > 0 ? confidence / significantCount : 0f;

            // Find appropriate template
            HeadlineTemplate selectedTemplate = null;
            foreach (var template in headlineTemplates)
            {
                if (confidence >= template.minConfidence)
                {
                    selectedTemplate = template;
                    break;
                }
            }

            if (selectedTemplate == null && headlineTemplates.Length > 0)
            {
                selectedTemplate = headlineTemplates[headlineTemplates.Length - 1]; // Use last as fallback
            }

            return selectedTemplate != null ? selectedTemplate.template : "新しいつながりを求めています";
        }

        private void OnValidate()
        {
            if (axisTemplates == null || axisTemplates.Length != 30)
            {
                var oldTemplates = axisTemplates;
                axisTemplates = new AxisTemplate[30];

                // Copy existing data
                if (oldTemplates != null)
                {
                    int copyCount = Mathf.Min(oldTemplates.Length, 30);
                    for (int i = 0; i < copyCount; i++)
                    {
                        axisTemplates[i] = oldTemplates[i];
                    }
                }

                // Initialize null entries
                for (int i = 0; i < 30; i++)
                {
                    if (axisTemplates[i] == null)
                    {
                        axisTemplates[i] = new AxisTemplate();
                        axisTemplates[i].axisName = $"軸{i + 1}";
                        axisTemplates[i].axisIndex = i;
                    }
                }
            }

            if (headlineTemplates == null)
            {
                headlineTemplates = new HeadlineTemplate[0];
            }
        }
    }
}