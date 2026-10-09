import math
import re
import unittest
from pathlib import Path

QUESTION_COUNT = 112
AXIS_COUNT = 30
TOLERANCE = 1e-4
RESPONSE_WEIGHTS = (-2.0, -1.0, 0.0, 1.0, 2.0)
ROOT = Path(__file__).resolve().parents[1]
VECTOR_BUILDER = ROOT / "Assets/VirtualTokyoMatching/Scripts/Vector/VectorBuilder.cs"
PUBLISHER = ROOT / "Assets/VirtualTokyoMatching/Scripts/Sync/PublicProfilePublisher.cs"


def config_weight(question_index: int, axis: int) -> float:
    # Deterministic non-symmetric fixture that exercises all 30 axes.
    return (((question_index + 3) * (axis + 5)) % 17 - 8) / 8.0


def rebuild_raw(responses):
    raw = [0.0] * AXIS_COUNT
    for question_index, response in enumerate(responses):
        if 1 <= response <= 5:
            response_weight = RESPONSE_WEIGHTS[response - 1]
            for axis in range(AXIS_COUNT):
                raw[axis] += response_weight * config_weight(question_index, axis)
    return raw


def normalize(raw, answered_count):
    if answered_count == 0:
        return [0.0] * AXIS_COUNT
    completion_ratio = answered_count / QUESTION_COUNT
    scaling_factor = 1.0 / max(completion_ratio, 0.1)
    scaled = [value * scaling_factor for value in raw]
    magnitude = math.sqrt(sum(value * value for value in scaled))
    if magnitude <= 1e-6:
        return [max(-1.0, min(1.0, value)) for value in raw]
    return [max(-1.0, min(1.0, value / magnitude)) for value in scaled]


def rebuild_normalized(responses):
    return normalize(rebuild_raw(responses), sum(value > 0 for value in responses))


def incremental_update(raw, responses, question_index, new_response):
    old_response = responses[question_index]
    if old_response == new_response:
        return list(raw), list(responses)

    updated_raw = list(raw)
    updated_responses = list(responses)
    if 1 <= old_response <= 5:
        old_weight = RESPONSE_WEIGHTS[old_response - 1]
        for axis in range(AXIS_COUNT):
            updated_raw[axis] -= old_weight * config_weight(question_index, axis)
    if 1 <= new_response <= 5:
        new_weight = RESPONSE_WEIGHTS[new_response - 1]
        for axis in range(AXIS_COUNT):
            updated_raw[axis] += new_weight * config_weight(question_index, axis)
    updated_responses[question_index] = new_response
    return updated_raw, updated_responses


def assert_vectors_close(testcase, left, right):
    testcase.assertEqual(len(left), len(right))
    for index, (lhs, rhs) in enumerate(zip(left, right)):
        testcase.assertLessEqual(abs(lhs - rhs), TOLERANCE, f"axis {index}: {lhs} != {rhs}")


def method_body(source: str, method_name: str) -> str:
    match = re.search(rf"\b{re.escape(method_name)}\s*\([^)]*\)\s*\{{", source)
    if not match:
        raise AssertionError(f"method not found: {method_name}")
    start = match.end() - 1
    depth = 0
    for index in range(start, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[start + 1:index]
    raise AssertionError(f"unterminated method: {method_name}")


class VectorRestoreEquivalenceTest(unittest.TestCase):
    def exercise_restore_edit(self, responses, question_index, new_response):
        # Persisted normalized output is deliberately present but is not raw authority.
        persisted_normalized = rebuild_normalized(responses)
        self.assertTrue(any(abs(value) > 0 for value in persisted_normalized))

        restored_raw = rebuild_raw(responses)
        incremental_raw, final_responses = incremental_update(
            restored_raw, responses, question_index, new_response
        )
        incremental_normalized = normalize(
            incremental_raw, sum(value > 0 for value in final_responses)
        )
        rebuilt_normalized = rebuild_normalized(final_responses)

        assert_vectors_close(self, incremental_raw, rebuild_raw(final_responses))
        assert_vectors_close(self, incremental_normalized, rebuilt_normalized)

    def test_incomplete_restore_edit_matches_full_rebuild(self):
        responses = [0] * QUESTION_COUNT
        for index in range(37):
            responses[index] = index % 5 + 1
        self.exercise_restore_edit(responses, 11, 5)

    def test_finalized_restore_edit_matches_full_rebuild(self):
        responses = [index % 5 + 1 for index in range(QUESTION_COUNT)]
        self.exercise_restore_edit(responses, 73, 1)

    def test_reapplying_same_answer_is_exactly_idempotent(self):
        responses = [index % 5 + 1 for index in range(QUESTION_COUNT)]
        raw = rebuild_raw(responses)
        updated_raw, updated_responses = incremental_update(raw, responses, 42, responses[42])
        self.assertEqual(raw, updated_raw)
        self.assertEqual(responses, updated_responses)

    def test_production_restore_path_rebuilds_raw_state(self):
        source = VECTOR_BUILDER.read_text(encoding="utf-8")
        body = method_body(source, "OnPlayerDataLoaded")
        self.assertIn("RebuildVectorFromResponses();", body)
        self.assertNotIn("GetVector30D", body)
        self.assertNotIn("savedVector", body)

    def test_production_paths_fail_closed_on_inconsistent_state(self):
        vector_source = VECTOR_BUILDER.read_text(encoding="utf-8")
        publisher_source = PUBLISHER.read_text(encoding="utf-8")

        update_body = method_body(vector_source, "UpdateVectorIncremental")
        finalize_body = method_body(vector_source, "FinalizeVector")
        getter_body = method_body(vector_source, "GetNormalizedVector")

        self.assertIn("if (oldResponse == response)", update_body)
        self.assertIn("IsVectorStateConsistent()", update_body)
        self.assertIn("IsVectorStateConsistent()", finalize_body)
        self.assertLess(finalize_body.index("IsVectorStateConsistent()"), finalize_body.index("UpdateVector30D"))
        self.assertIn("IsVectorStateConsistent()", getter_body)
        self.assertIn("return null;", getter_body)
        self.assertIn("vectorBuilder.GetNormalizedVector()", publisher_source)
        self.assertRegex(publisher_source, r"vector30D\s*==\s*null\s*\|\|\s*vector30D\.Length\s*!=\s*30")


if __name__ == "__main__":
    unittest.main()
