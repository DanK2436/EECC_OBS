import React from "react";
import { SceneSource } from "../../types/studio";
import { Plus, Eye, EyeOff, Lock, Unlock, Smartphone, Camera, Mic, Presentation, BookOpen, Monitor } from "lucide-react";

interface SourcesPanelProps {
  sources: SceneSource[];
  onToggleVisibility: (id: string) => void;
  onToggleLock: (id: string) => void;
  onAddSource: () => void;
  onPairMobileCamera: () => void;
}

export const SourcesPanel: React.FC<SourcesPanelProps> = ({
  sources,
  onToggleVisibility,
  onToggleLock,
  onAddSource,
  onPairMobileCamera,
}) => {
  const getSourceIcon = (type: SceneSource["type"]) => {
    switch (type) {
      case "mobile_camera":
        return <Smartphone className="w-3.5 h-3.5 text-blue-500" />;
      case "physical_camera":
        return <Camera className="w-3.5 h-3.5 text-slate-600" />;
      case "audio_input":
        return <Mic className="w-3.5 h-3.5 text-emerald-600" />;
      case "presentation_slide":
        return <Presentation className="w-3.5 h-3.5 text-indigo-500" />;
      case "bible_verse":
        return <BookOpen className="w-3.5 h-3.5 text-amber-600" />;
      default:
        return <Monitor className="w-3.5 h-3.5 text-slate-500" />;
    }
  };

  return (
    <div className="w-72 bg-surface border-r border-border flex flex-col h-full select-none">
      {/* En-tête Panneau */}
      <div className="h-8 px-3 bg-panel border-b border-border flex items-center justify-between">
        <span className="text-xs font-bold text-slate-700">Sources de la scène</span>
        <div className="flex items-center space-x-1">
          <button
            onClick={onPairMobileCamera}
            title="Appairer EECC Caméra (Smartphone)"
            className="px-2 py-0.5 rounded bg-blue-100 hover:bg-blue-200 text-primary text-[10px] font-bold transition-colors flex items-center"
          >
            <Smartphone className="w-3 h-3 mr-1" />
            + Tél.
          </button>
          <button
            onClick={onAddSource}
            title="Ajouter une source"
            className="p-1 rounded hover:bg-slate-200 text-slate-600 hover:text-textMain transition-colors"
          >
            <Plus className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* Liste des Sources */}
      <div className="flex-1 overflow-y-auto p-1.5 space-y-1">
        {sources.length === 0 ? (
          <div className="text-center py-8 text-xs text-slate-400">
            Aucune source dans cette scène.
          </div>
        ) : (
          sources.map((src) => (
            <div
              key={src.id}
              className={`px-2.5 py-2 rounded-lg border text-xs flex items-center justify-between transition-colors ${
                src.visible
                  ? "bg-white border-slate-200 text-slate-800 shadow-xs"
                  : "bg-slate-50 border-slate-100 text-slate-400"
              }`}
            >
              <div className="flex items-center space-x-2 truncate">
                {getSourceIcon(src.type)}
                <span className="font-semibold truncate">{src.name}</span>
                {src.type === "mobile_camera" && src.telemetry?.battery && (
                  <span className="text-[10px] text-green-600 bg-green-50 px-1 rounded font-mono">
                    {src.telemetry.battery}%
                  </span>
                )}
              </div>

              <div className="flex items-center space-x-1 text-slate-400">
                <button
                  onClick={() => onToggleLock(src.id)}
                  className="p-1 hover:text-slate-700 rounded"
                >
                  {src.locked ? (
                    <Lock className="w-3 h-3 text-amber-500" />
                  ) : (
                    <Unlock className="w-3 h-3 text-slate-300" />
                  )}
                </button>
                <button
                  onClick={() => onToggleVisibility(src.id)}
                  className="p-1 hover:text-slate-700 rounded"
                >
                  {src.visible ? (
                    <Eye className="w-3.5 h-3.5 text-slate-600" />
                  ) : (
                    <EyeOff className="w-3.5 h-3.5 text-slate-300" />
                  )}
                </button>
              </div>
            </div>
          ))
        )}
      </div>

      <div className="h-8 px-3 bg-panel border-t border-border flex items-center justify-between text-[10px] text-slate-400">
        <span>Ordre d'incrustation</span>
        <span>{sources.length} sources</span>
      </div>
    </div>
  );
};
