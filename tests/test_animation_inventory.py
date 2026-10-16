import unittest
from tools.map_arena_animations import emitted_annotations


class AnimationInventoryTests(unittest.TestCase):
    def test_journal_conditional_and_initializer_emissions(self):
        source='''
            Type = { AnnotationType.Attachment };
            annotation.Type.Add(AnnotationType.DamageDealt);
            annotation.Type.Add(kind == "start" ? AnnotationType.ResolutionStart : AnnotationType.ResolutionComplete);
            Type={delta>0?AnnotationType.CounterAdded:AnnotationType.CounterRemoved};
        '''
        self.assertEqual(emitted_annotations([source]), {
            'Attachment','DamageDealt','ResolutionStart','ResolutionComplete','CounterAdded','CounterRemoved'})

    def test_comparisons_and_commented_out_adapters_are_not_support(self):
        source='''
            state.Annotations.Where(a=>a.Type.Contains(AnnotationType.Scry));
            // Type = {AnnotationType.Shuffle};
            /* annotation.Type.Add(AnnotationType.CoinFlip); */
        '''
        self.assertEqual(emitted_annotations([source]),set())


if __name__=='__main__':unittest.main()
