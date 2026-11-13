import React from "react";
import { Volume2 } from "lucide-react";
import type { AudioChannel } from "../../types/studio";
import { DEMO_METER_LEVELS, gainLabel } from "../../lib/studioMetrics";

interface AudioMixerPanelProps {
  channels: AudioChannel[];
  demo: boolean;
  onVolumeChange: (id: string, vol: number) => void;
  onToggleMute: (id: string) => void;
}

const SEGMENTS = 16;

function Meter({ level }: { level: number }) {
  const lit = Math.round(Math.max(0, Math.min(1, level)) * SEGMENTS);
  return (
    <div className="vu" aria-hidden>
      {Array.from({ length: SEGMENTS }, (_, i) => {
        const on = i < lit;
        const zone = i >= 13 ? "r" : i >= 10 ? "y" : "g";
        return <i key={i} className={on ? `lit ${zone}` : ""} />;
      })}
    </div>
  );
}

export const AudioMixerPanel: React.FC<AudioMixerPanelProps> = ({
  channels,
  demo,
  onVolumeChange,
  onToggleMute,
}) => {
  return (
    <section className="dock" aria-label="Mixeur audio">
      <header className="dock-head">
        <span style={{ display: "inline-flex", alignItems: "center", gap: 5 }}>
          <Volume2 size={13} /> Mixeur Audio
        </span>
      </header>
      <div className="dock-body">
        <div className="mixer">
          {channels.map((chan) => {
            const demoLevel = DEMO_METER_LEVELS[chan.id] ?? 0;
            const level = chan.muted ? 0 : demo ? demoLevel : chan.peakLevel;
            return (
              <div key={chan.id} className={`chan ${chan.id === "master" ? "master" : ""}`}>
                <div className="nm">{chan.name}</div>
                <div className="meter-row">
                  <Meter level={level} />
                  <Meter level={Math.max(0, level - 0.06)} />
                  <input
                    className="fader"
                    type="range"
                    min={0}
                    max={100}
                    step={0.1}
                    value={chan.volume}
                    aria-label={`Gain ${chan.name}`}
                    onChange={(e) => onVolumeChange(chan.id, Number(e.target.value))}
                  />
                </div>
                <div className="db">{gainLabel(chan.volume)}</div>
                <button type="button" className={`mute ${chan.muted ? "on" : ""}`} onClick={() => onToggleMute(chan.id)}>
                  MUTE
                </button>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
};
