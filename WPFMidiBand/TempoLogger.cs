using System;

namespace WPFMidiBand {
    class TempoLogger : IComparable<TempoLogger> {
        public int Tempo { get; private set; }
        public int Tick { get; private set; }
        public long TimeSum;
        public TempoLogger(int tempo, int tick) {
            Tempo = tempo;
            Tick = tick;
        }

        public int CompareTo(TempoLogger other) {
            if (Tick != other.Tick) {
                return Tick - other.Tick;
            } else {
                return Tempo - other.Tempo;
            }
        }
    }
}
