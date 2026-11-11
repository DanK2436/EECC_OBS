import React from "react";
import { StudioScene } from "../../types/studio";
import { ArrowRight, SlidersHorizontal, RefreshCw } from "lucide-react";

interface StudioPreviewProgramProps {
  previewScene: StudioScene;
  programScene: StudioScene;
  studioMode: boolean;
  transitionDurationMs: number;
  onSetTransitionDuration: (val: number) => void;
  onExecuteTransition: (type: "cut" | "fade") => void;
}

export const StudioPreviewProgram: React.FC<StudioPreviewProgramProps> = ({
  previewScene,
  programScene,
  studioMode,
  transitionDurationMs,
  onSetTransitionDuration,
  onExecuteTransition,
}) => {
  return (
    <div className="flex-1 bg-slate-950 p-4 flex items-center justify-center gap-4 overflow-hidden">
      {/* 1. ÉCRAN APERÇU (PREVIEW - Cadre Vert) */}
      {studioMode && (
        <div className="flex-1 max-w-xl h-full flex flex-col">
          <div className="flex items-center justify-between pb-1.5 px-1">
            <span className="text-xs font-bold text-green-400 uppercase tracking-wider flex items-center">
              <span className="w-2 h-2 rounded-full bg-green-500 mr-2" />
              Aperçu (Prochaine Scène) : {previewScene.name}
            </span>
            <span className="text-[11px] text-slate-500 font-mono">1920x1080</span>
          </div>

          <div className="flex-1 bg-slate-900 rounded-xl border-2 border-green-600/70 overflow-hidden relative shadow-lg flex items-center justify-center">
            {/* Visualisation simulée de la composition vidéo */}
            <div className="text-center text-slate-400">
              <span className="text-xl font-bold text-slate-200 block mb-1">{previewScene.name}</span>
              <span className="text-xs text-slate-500">
                {previewScene.sources.filter((s) => s.visible).length} sources actives
              </span>
            </div>

            {/* Repère de cadrage */}
            <div className="absolute inset-4 border border-dashed border-white/10 pointer-events-none" />
          </div>
        </div>
      )}

      {/* 2. CONTRÔLES DE TRANSITION CENTRAUX */}
      {studioMode && (
        <div className="flex flex-col items-center justify-center space-y-3 px-2">
          <button
            onClick={() => onExecuteTransition("cut")}
            className="w-24 py-2 bg-slate-800 hover:bg-slate-700 text-white rounded-lg text-xs font-bold transition-all shadow-md active:scale-95 border border-slate-700"
          >
            CUT
          </button>

          <button
            onClick={() => onExecuteTransition("fade")}
            className="w-24 py-2.5 bg-primary hover:bg-primary-light text-white rounded-lg text-xs font-bold transition-all shadow-md shadow-primary/30 active:scale-95 flex items-center justify-center space-x-1"
          >
            <span>FONDU</span>
            <ArrowRight className="w-3.5 h-3.5" />
          </button>

          {/* Durée de transition */}
          <div className="flex flex-col items-center mt-2">
            <span className="text-[10px] text-slate-400 font-semibold mb-1">DURÉE</span>
            <div className="flex items-center bg-slate-800 rounded px-2 py-1 border border-slate-700 text-xs">
              <input
                type="number"
                value={transitionDurationMs}
                onChange={(e) => onSetTransitionDuration(Number(e.target.value))}
                className="w-12 bg-transparent text-center text-white font-mono focus:outline-none"
              />
              <span className="text-slate-500 text-[10px]">ms</span>
            </div>
          </div>
        </div>
      )}

      {/* 3. ÉCRAN PROGRAMME (PROGRAM - Cadre Rouge / À l'antenne) */}
      <div className="flex-1 max-w-xl h-full flex flex-col">
        <div className="flex items-center justify-between pb-1.5 px-1">
          <span className="text-xs font-bold text-live uppercase tracking-wider flex items-center">
            <span className="w-2 h-2 rounded-full bg-live mr-2 animate-ping" />
            Programme (À l’antenne) : {programScene.name}
          </span>
          <span className="text-[11px] text-slate-500 font-mono">1920x1080 @ 60fps</span>
        </div>

        <div className="flex-1 bg-slate-900 rounded-xl border-2 border-live overflow-hidden relative shadow-lg flex items-center justify-center">
          {/* Signal réel envoyé aux projecteurs et aux diffusions RTMP */}
          <div className="text-center text-slate-400">
            <span className="text-xl font-bold text-white block mb-1">{programScene.name}</span>
            <span className="text-xs text-live font-semibold">SIGNAL ENVOYÉ SUR LE FLUX</span>
          </div>

          {/* Filigrane d'identification */}
          <div className="absolute bottom-3 right-4 px-2 py-0.5 rounded bg-black/60 text-[10px] text-white/70 font-mono">
            EECC TV LIVE
          </div>
        </div>
      </div>
    </div>
  );
};
