using UnityEngine;
using System;

namespace VirtualTokyoMatching
{
    /// <summary>
    /// ScriptableObject containing the 112 questions for the personality assessment.
    /// Allows for runtime-free updates to questions and their weights.
    /// </summary>
    [CreateAssetMenu(fileName = "QuestionDatabase", menuName = "VTM/Question Database")]
    public class QuestionDatabase : ScriptableObject
    {
        [System.Serializable]
        public class Question
        {
            [TextArea(3, 5)]
            public string text;
            public string[] choices = new string[5];
            public int targetAxis; // 0-29 for the 30 axes
            public float[] weights = new float[5]; // weights for choices 1-5
        }

        [Header("Assessment Questions")]
        public Question[] questions = new Question[112];

        public bool ValidateDatabase()
        {
            if (questions.Length != 112)
            {
                Debug.LogError($"Question database must contain exactly 112 questions. Current count: {questions.Length}");
                return false;
            }

            for (int i = 0; i < questions.Length; i++)
            {
                var q = questions[i];
                if (q == null)
                {
                    Debug.LogError($"Question {i + 1} is null");
                    return false;
                }

                if (string.IsNullOrEmpty(q.text))
                {
                    Debug.LogError($"Question {i + 1} has empty text");
                    return false;
                }

                if (q.targetAxis < 0 || q.targetAxis >= 30)
                {
                    Debug.LogError($"Question {i + 1} has invalid target axis: {q.targetAxis}");
                    return false;
                }

                if (q.choices.Length != 5 || q.weights.Length != 5)
                {
                    Debug.LogError($"Question {i + 1} must have exactly 5 choices and weights");
                    return false;
                }
            }

            return true;
        }

        private void OnValidate()
        {
            if (questions != null && questions.Length != 112)
            {
                Array.Resize(ref questions, 112);
            }

            // Initialize null questions
            for (int i = 0; i < questions.Length; i++)
            {
                if (questions[i] == null)
                {
                    questions[i] = new Question();
                    questions[i].choices = new string[5];
                    questions[i].weights = new float[5];
                }
            }
        }
    }
}