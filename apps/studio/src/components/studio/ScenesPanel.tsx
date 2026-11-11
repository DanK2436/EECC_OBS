import React from "react";
import { StudioScene, SceneId } from "../../types/studio";
import { Plus, Trash2, Copy, MoveUp, MoveDown, Layers } from "lucide-react";

interface ScenesPanelProps {
  scenes: StudioScene[];
  selectedSceneId: SceneId;
  onSelectScene: (id: SceneId) => void;
  onAddScene: () => void;
  onDeleteScene: (id: SceneId) => void;
}

export const ScenesPanel: React.FC<ScenesPanelProps> = ({
  scenes,
  selectedSceneId,
  onSelectScene,
  onAddScene,
  onDeleteScene,
}) => {
  return (
    <div className="w-56 bg-surface border-r border-border flex flex-col h-full select-none">
      {/* En-tête Panneau */}
      <div className="h-8 px-3 bg-panel border-b border-border flex items-center justify-between">
        <span className="text-xs font-bold text-slate-700 flex items-center">
          <Layers className="w-3.5 h-3.5 mr-1.5 text-primary" />
          Scènes
        </span>
        <div className="flex items-center space-x-1">
          <button
            onClick={onAddScene}
            title="Ajouter une scène"
            className="p-1 rounded hover:bg-slate-200 text-slate-600 hover:text-textMain transition-colors"
          >
            <Plus className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* Liste des Scènes */}
      <div className="flex-1 overflow-y-auto p-1.5 space-y-1">
        {scenes.map((scene) => {
          const isSelected = scene.id === selectedSceneId;
          return (
            <div
              key={scene.id}
              onClick={() => onSelectScene(scene.id)}
              className={`px-3 py-2 rounded-lg text-xs font-semibold cursor-pointer flex items-center justify-between transition-colors ${
                isSelected
                  ? "bg-primary text-white shadow-sm"
                  : "text-slate-700 hover:bg-slate-100"
              }`}
            >
              <span className="truncate">{scene.name}</span>
              {isSelected && (
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    if (scenes.length > 1) onDeleteScene(scene.id);
                  }}
                  className="opacity-80 hover:opacity-100 p-0.5 rounded hover:bg-primary-dark"
                >
                  <Trash2 className="w-3 h-3 text-white" />
                </button>
              )}
            </div>
          );
        })}
      </div>

      {/* Outils Scènes en bas */}
      <div className="h-8 px-2 bg-panel border-t border-border flex items-center justify-between text-slate-500">
        <div className="flex items-center space-x-1">
          <button className="p-1 rounded hover:bg-slate-200 hover:text-textMain">
            <MoveUp className="w-3.5 h-3.5" />
          </button>
          <button className="p-1 rounded hover:bg-slate-200 hover:text-textMain">
            <MoveDown className="w-3.5 h-3.5" />
          </button>
        </div>
        <span className="text-[10px] font-mono text-slate-400">{scenes.length} scènes</span>
      </div>
    </div>
  );
};
