using Sanford.Multimedia.Midi;
using System.Collections.Generic;
using System.Reflection;

namespace WPFMidiBand {
    class BlackMagic {
        /*
        private static readonly FieldInfo sequencerEnumField = typeof(Sequencer).GetField("enumerators", BindingFlags.NonPublic | BindingFlags.Instance);
        public static void ResetSequencer(Sequencer seq) {
            sequencerEnumField.SetValue(seq, new List<IEnumerator<int>>());
        }
        */
        private static readonly FieldInfo sequencerClockField = typeof(Sequencer).GetField("clock", BindingFlags.NonPublic | BindingFlags.Instance);
        public static MidiInternalClock GetClock(Sequencer seq) {
            return sequencerClockField.GetValue(seq) as MidiInternalClock;
        }
    }
}
