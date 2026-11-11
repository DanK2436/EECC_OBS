import React from "react";
import { AudioChannel } from "../../types/studio";
import { Sliders, Volume2, VolumeX, SlidersHorizontal } from "lucide-react";

interface AudioMixerPanelProps {
  channels: AudioChannel[];
  onVolumeChange: (id: string, vol: number) => void;
  onToggleMute: (id: string) => void;
  onOpenDspFilters: (channel: AudioChannel) => void;
}

export const AudioMixerPanel: React.FC<AudioMixerPanelProps> = ({
  channels,
  onVolumeChange,
  onToggleMute,
  onOpenDspFilters,
}) => {
  return (
    <div className="flex-1 bg-surface border-r border-border flex flex-col h-full select-none overflow-hidden">
      {/* En-tête Panneau */}
      <div className="h-8 px-3 bg-panel border-b border-border flex items-center justify-between">
        <span className="text-xs font-bold text-slate-700 flex items-center">
          <Sliders className="w-3.5 h-3.5 mr-1.5 text-primary" />
          Mixeur Audio Professionnel (DSP)
        </span>
        <span className="text-[10px] text-slate-400">48 kHz • 24 bits</span>
      </div>

      {/* Tranches Audio Horizontales/Verticales */}
      <div className="flex-1 overflow-x-auto p-3 flex space-x-3 items-stretch">
        {channels.map((chan) => {
          const isMaster = chan.id === "master";

          return (
            <div
              key={chan.id}
              className={`w-28 rounded-xl p-2.5 flex flex-col justify-between border ${
                isMaster
                  ? "bg-slate-900 text-white border-slate-700"
                  : "bg-slate-50 text-slate-800 border-slate-200"
              }`}
            >
              {/* Nom du canal */}
              <div className="text-center">
                <span className="text-[11px] font-bold block truncate" title={chan.name}>
                  {chan.name}
                </span>
                <span
                  className={`text-[9px] font-mono ${
                    isMaster ? "text-slate-400" : "text-slate-500"
                  }`}
                >
                  {chan.volume}%
                </span>
              </div>

              {/* VU-Mètre et Fader */}
              <div className="flex-1 my-2 flex items-center justify-center space-x-2">
                {/* VU-Mètre Dynamique (Peak / RMS) */}
                <div className="w-3 h-32 bg-slate-200 rounded-sm overflow-hidden relative border border-slate-300 flex flex-col justify-end">
                  <div
                    className="w-full transition-all duration-75 rounded-sm"
                    style={{
                      height: `${chan.muted ? 0 : chan.peakLevel * 100}%`,
                      background: "linear-gradient(to top, #16A34A 60%, #EAB308 85%, #DC2626 100%)",
                    }}
                  />
                </div>

                {/* Fader Vertical */}
                <div className="h-32 flex items-center">
                  <input
                    type="range"
                    min="0"
                    max="100"
                    value={chan.volume}
                    onChange={(e) => onVolumeChange(chan.id, Number(e.target.value))}
                    className="h-28 w-2 appearance-none bg-slate-300 rounded cursor-pointer accent-primary"
                    style={{ writingMode: "vertical-lr", direction: "rtl" }}
                  />
                </div>
              </div>

              {/* Boutons Mute & DSP Filtres */}
              <div className="space-y-1">
                <button
                  onClick={() => onToggleMute(chan.id)}
                  className={`w-full py-1 rounded text-[10px] font-bold flex items-center justify-center transition-colors ${
                    chan.muted
                      ? "bg-red-600 text-white"
                      : isMaster
                      ? "bg-slate-800 hover:bg-slate-700 text-slate-300"
                      : "bg-white hover:bg-slate-100 text-slate-700 border border-slate-200"
                  }`}
                >
                  {chan.muted ? (
                    <>
                      <VolumeX className="w-3 h-3 mr-1" /> MUTE
                    </>
                  ) : (
                    <>
                      <Volume2 className="w-3 h-3 mr-1" /> ON
                    </>
                  )}
                </button>

                {!isMaster && (
                  <button
                    onClick={() => onOpenDspFilters(chan)}
                    className="w-full py-0.5 rounded text-[9px] font-semibold bg-slate-200 hover:bg-slate-300 text-slate-700 flex items-center justify-center transition-colors"
                  >
                    <SlidersHorizontal className="w-2.5 h-2.5 mr-1" /> Filtres EQ
                  </button>
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
