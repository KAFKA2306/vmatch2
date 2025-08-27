using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System.Collections.Generic;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// Generates automatic personality summaries and tags from 30D vectors.
    /// Uses template-based generation for consistent, localized output.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class ValuesSummaryGenerator : UdonSharpBehaviour
    {
        [Header("Dependencies")]
        public SummaryTemplates summaryTemplates;
        public VectorConfiguration vectorConfig;

        [Header("Generation Settings")]
        [Range(0.1f, 1f)]
        public float significanceThreshold = 0.3f;
        
        [Range(1, 5)]
        public int maxTagsToGenerate = 3;
        
        [Range(1, 3)]
        public int maxSummaryLines = 2;

        [Header("Localization")]
        public bool useJapanese = true;
        
        // Fallback templates when SummaryTemplates is not configured
        private readonly string[] fallbackPositiveTags = new string[]
        {
            "積極的", "創造的", "協調的", "論理的", "感性豊か", "責任感強い"
        };
        
        private readonly string[] fallbackNegativeTags = new string[]
        {
            "慎重派", "独立志向", "個人主義", "直感派", "現実的", "自由志向"
        };
        
        private readonly string[] fallbackSummaryTemplates = new string[]
        {
            "{trait1}で{trait2}な価値観をお持ちです",
            "{trait1}的な考え方を重視される方です",
            "バランスの取れた価値観をお持ちの方です"
        };

        void Start()
        {
            // Load configuration
            if (summaryTemplates != null)
            {
                significanceThreshold = summaryTemplates.significanceThreshold;
                maxTagsToGenerate = summaryTemplates.maxTagsPerProfile;
                maxSummaryLines = summaryTemplates.maxSentencesInSummary;
            }
        }

        /// <summary>
        /// Generate complete profile summary from 30D vector
        /// </summary>
        public ProfileSummary GenerateProfileSummary(float[] vector30D)
        {
            if (vector30D == null || vector30D.Length != 30)
            {
                return CreateFallbackSummary();
            }

            var summary = new ProfileSummary();
            
            // Analyze vector for significant traits
            var significantTraits = AnalyzeSignificantTraits(vector30D);
            
            // Generate components
            summary.tags = GenerateTagsFromTraits(significantTraits);
            summary.summaryText = GenerateSummaryFromTraits(significantTraits);
            summary.headline = GenerateHeadlineFromTraits(significantTraits);
            summary.confidence = CalculateConfidence(vector30D);
            
            return summary;
        }

        /// <summary>
        /// Generate tags array from vector
        /// </summary>
        public string[] GenerateTags(float[] vector30D)
        {
            if (summaryTemplates != null)
            {
                return summaryTemplates.GenerateTags(vector30D);
            }

            // Fallback implementation
            return GenerateFallbackTags(vector30D);
        }

        /// <summary>
        /// Generate summary text from vector
        /// </summary>
        public string GenerateSummary(float[] vector30D)
        {
            if (summaryTemplates != null)
            {
                return summaryTemplates.GenerateSummary(vector30D);
            }

            // Fallback implementation
            return GenerateFallbackSummary(vector30D);
        }

        /// <summary>
        /// Generate headline from vector
        /// </summary>
        public string GenerateHeadline(float[] vector30D)
        {
            if (summaryTemplates != null)
            {
                return summaryTemplates.GenerateHeadline(vector30D);
            }

            // Fallback implementation
            return GenerateFallbackHeadline(vector30D);
        }

        private List<TraitAnalysis> AnalyzeSignificantTraits(float[] vector30D)
        {
            var traits = new List<TraitAnalysis>();

            for (int i = 0; i < vector30D.Length; i++)
            {
                float value = vector30D[i];
                if (Mathf.Abs(value) >= significanceThreshold)
                {
                    var trait = new TraitAnalysis();
                    trait.axisIndex = i;
                    trait.value = value;
                    trait.strength = Mathf.Abs(value);
                    trait.isPositive = value > 0;
                    
                    // Get axis name
                    if (vectorConfig != null)
                    {
                        trait.axisName = vectorConfig.GetAxisName(i);
                    }
                    else
                    {
                        trait.axisName = $"特性{i + 1}";
                    }

                    traits.Add(trait);
                }
            }

            // Sort by strength (most significant first)
            traits.Sort((a, b) => b.strength.CompareTo(a.strength));

            return traits;
        }

        private string[] GenerateTagsFromTraits(List<TraitAnalysis> traits)
        {
            var tags = new List<string>();
            
            foreach (var trait in traits)
            {
                if (tags.Count >= maxTagsToGenerate) break;

                string tag = GetTagForTrait(trait);
                if (!string.IsNullOrEmpty(tag) && !tags.Contains(tag))
                {
                    tags.Add(tag);
                }
            }

            // Fill with fallback tags if needed
            while (tags.Count < maxTagsToGenerate && tags.Count < fallbackPositiveTags.Length)
            {
                int randomIndex = Random.Range(0, fallbackPositiveTags.Length);
                string fallbackTag = fallbackPositiveTags[randomIndex];
                if (!tags.Contains(fallbackTag))
                {
                    tags.Add(fallbackTag);
                }
            }

            return tags.ToArray();
        }

        private string GenerateSummaryFromTraits(List<TraitAnalysis> traits)
        {
            if (traits.Count == 0)
            {
                return useJapanese ? "バランスの取れた価値観をお持ちです。" : "Well-balanced personality.";
            }

            var sentences = new List<string>();
            
            // Generate sentences based on top traits
            int sentenceCount = 0;
            foreach (var trait in traits)
            {
                if (sentenceCount >= maxSummaryLines) break;

                string sentence = GetSentenceForTrait(trait);
                if (!string.IsNullOrEmpty(sentence))
                {
                    sentences.Add(sentence);
                    sentenceCount++;
                }
            }

            if (sentences.Count == 0)
            {
                sentences.Add(useJapanese ? "多面的な価値観をお持ちの方です。" : "Multi-faceted personality.");
            }

            return string.Join(useJapanese ? "。" : ". ", sentences) + (useJapanese ? "。" : ".");
        }

        private string GenerateHeadlineFromTraits(List<TraitAnalysis> traits)
        {
            if (traits.Count == 0)
            {
                return useJapanese ? "新しいつながりを求めています" : "Looking for new connections";
            }

            // Use the strongest trait for headline
            var primaryTrait = traits[0];
            return GetHeadlineForTrait(primaryTrait);
        }

        private string GetTagForTrait(TraitAnalysis trait)
        {
            // This would ideally use the SummaryTemplates configuration
            // For now, use a simple mapping based on axis index and polarity
            
            if (trait.isPositive)
            {
                return GetPositiveTagForAxis(trait.axisIndex);
            }
            else
            {
                return GetNegativeTagForAxis(trait.axisIndex);
            }
        }

        private string GetSentenceForTrait(TraitAnalysis trait)
        {
            string intensifier = GetIntensifierForStrength(trait.strength);
            string traitDescription = GetTraitDescription(trait);
            
            if (useJapanese)
            {
                return $"{intensifier}{traitDescription}な価値観を重視されます";
            }
            else
            {
                return $"Values {intensifier} {traitDescription} approach";
            }
        }

        private string GetHeadlineForTrait(TraitAnalysis trait)
        {
            string traitName = GetTraitDescription(trait);
            
            if (useJapanese)
            {
                return $"{traitName}を大切にする方です";
            }
            else
            {
                return $"Values {traitName}";
            }
        }

        private string GetPositiveTagForAxis(int axisIndex)
        {
            // Map axis indices to positive trait tags
            // This is a simplified implementation - real version would use SummaryTemplates
            string[] axisTags = new string[]
            {
                "外向的", "創造的", "協調的", "論理的", "感情豊か", "責任感強い",
                "冒険好き", "理想主義", "社交的", "楽観的", "独立的", "献身的",
                "革新的", "忍耐強い", "表現力豊か", "分析的", "共感的", "積極的",
                "多様性重視", "計画的", "直感的", "競争的", "協力的", "自由志向",
                "現実的", "情熱的", "慎重", "柔軟性", "伝統重視", "進歩的"
            };
            
            return axisIndex < axisTags.Length ? axisTags[axisIndex] : "特徴的";
        }

        private string GetNegativeTagForAxis(int axisIndex)
        {
            // Map axis indices to negative trait tags (opposite polarity)
            string[] axisTags = new string[]
            {
                "内向的", "実用的", "個人主義", "直感派", "理性的", "自由奔放",
                "安定志向", "現実主義", "少数精鋭", "慎重派", "協調性", "自立志向",
                "保守的", "即断即決", "控えめ", "直感的", "客観的", "受動的",
                "一貫性重視", "柔軟対応", "論理的", "協力的", "独立志向", "規律重視",
                "理想的", "冷静", "大胆", "一貫性", "革新的", "伝統的"
            };
            
            return axisIndex < axisTags.Length ? axisTags[axisIndex] : "独特";
        }

        private string GetTraitDescription(TraitAnalysis trait)
        {
            if (trait.isPositive)
            {
                return GetPositiveTraitDescription(trait.axisIndex);
            }
            else
            {
                return GetNegativeTraitDescription(trait.axisIndex);
            }
        }

        private string GetPositiveTraitDescription(int axisIndex)
        {
            if (useJapanese)
            {
                string[] descriptions = new string[]
                {
                    "社交的", "創造性", "協調性", "論理性", "感情表現", "責任感",
                    "冒険心", "理想追求", "コミュニケーション", "楽観性", "自立性", "献身性",
                    "革新性", "持続力", "表現力", "分析力", "共感性", "積極性",
                    "多様性", "計画性", "直感力", "競争心", "協力性", "自由性",
                    "現実性", "情熱", "慎重さ", "適応力", "伝統", "進歩性"
                };
                return axisIndex < descriptions.Length ? descriptions[axisIndex] : "特徴的";
            }
            else
            {
                return "characteristic"; // English fallback
            }
        }

        private string GetNegativeTraitDescription(int axisIndex)
        {
            if (useJapanese)
            {
                string[] descriptions = new string[]
                {
                    "内省的", "実践性", "独立性", "直感性", "理性", "自由度",
                    "安定性", "現実性", "選択性", "慎重性", "協調性", "自立性",
                    "伝統性", "迅速性", "控えめさ", "直感性", "客観性", "受容性",
                    "一貫性", "柔軟性", "論理性", "協力性", "個人性", "規律性",
                    "理想性", "冷静さ", "大胆さ", "一貫性", "革新性", "伝統性"
                };
                return axisIndex < descriptions.Length ? descriptions[axisIndex] : "独特";
            }
            else
            {
                return "distinctive"; // English fallback
            }
        }

        private string GetIntensifierForStrength(float strength)
        {
            if (useJapanese)
            {
                if (strength > 0.8f) return "非常に";
                if (strength > 0.6f) return "とても";
                if (strength > 0.4f) return "やや";
                return "";
            }
            else
            {
                if (strength > 0.8f) return "highly";
                if (strength > 0.6f) return "very";
                if (strength > 0.4f) return "somewhat";
                return "";
            }
        }

        private float CalculateConfidence(float[] vector30D)
        {
            // Calculate confidence based on vector magnitude and number of significant traits
            float magnitude = 0f;
            int significantCount = 0;

            for (int i = 0; i < vector30D.Length; i++)
            {
                magnitude += vector30D[i] * vector30D[i];
                if (Mathf.Abs(vector30D[i]) >= significanceThreshold)
                {
                    significantCount++;
                }
            }

            magnitude = Mathf.Sqrt(magnitude);
            float normalizedMagnitude = Mathf.Clamp01(magnitude);
            float traitDiversity = Mathf.Clamp01((float)significantCount / 10f); // Normalize to 10 traits

            return (normalizedMagnitude + traitDiversity) * 0.5f;
        }

        // Fallback methods when SummaryTemplates is not available
        private string[] GenerateFallbackTags(float[] vector30D)
        {
            var tags = new List<string>();
            var traits = AnalyzeSignificantTraits(vector30D);

            foreach (var trait in traits)
            {
                if (tags.Count >= maxTagsToGenerate) break;
                
                if (trait.isPositive && tags.Count < fallbackPositiveTags.Length)
                {
                    tags.Add(fallbackPositiveTags[trait.axisIndex % fallbackPositiveTags.Length]);
                }
                else if (!trait.isPositive && tags.Count < fallbackNegativeTags.Length)
                {
                    tags.Add(fallbackNegativeTags[trait.axisIndex % fallbackNegativeTags.Length]);
                }
            }

            return tags.ToArray();
        }

        private string GenerateFallbackSummary(float[] vector30D)
        {
            var traits = AnalyzeSignificantTraits(vector30D);
            
            if (traits.Count == 0)
            {
                return "バランスの取れた価値観をお持ちです。";
            }

            string trait1 = GetTagForTrait(traits[0]);
            string trait2 = traits.Count > 1 ? GetTagForTrait(traits[1]) : "";

            if (!string.IsNullOrEmpty(trait2))
            {
                return $"{trait1}で{trait2}な価値観をお持ちです。";
            }
            else
            {
                return $"{trait1}的な考え方を重視される方です。";
            }
        }

        private string GenerateFallbackHeadline(float[] vector30D)
        {
            var traits = AnalyzeSignificantTraits(vector30D);
            
            if (traits.Count == 0)
            {
                return "新しいつながりを求めています";
            }

            string primaryTrait = GetTagForTrait(traits[0]);
            return $"{primaryTrait}な方です";
        }

        private ProfileSummary CreateFallbackSummary()
        {
            var summary = new ProfileSummary();
            summary.tags = new string[] { "バランス型", "多面的", "個性的" };
            summary.summaryText = "多面的な価値観をお持ちの方です。";
            summary.headline = "新しいつながりを求めています";
            summary.confidence = 0.5f;
            return summary;
        }

        [System.Serializable]
        public class ProfileSummary
        {
            public string[] tags;
            public string summaryText;
            public string headline;
            public float confidence;
        }

        [System.Serializable]
        private class TraitAnalysis
        {
            public int axisIndex;
            public float value;
            public float strength;
            public bool isPositive;
            public string axisName;
        }
    }
}