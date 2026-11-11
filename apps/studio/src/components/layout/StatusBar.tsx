import React from "react";
import { Radio, Disc, Cpu, HardDrive, Wifi, Activity } from "lucide-react";

interface StatusBarProps {
  isLive: boolean;
  isRecording: boolean;
  liveDuration: string;
  recordDuration: string;
  cpuUsage: number;
  memoryUsageMb: number;
  fps: number;
  droppedFrames: number;
  bitrateKbps: number;
}

export const StatusBar: React.FC<StatusBarProps> = ({
  isLive,
  isRecording,
  liveDuration,
  recordDuration,
  cpuUsage,
  memoryUsageMb,
  fps,
  droppedFrames,
  bitrateKbps,
}) => {
  return (
    <footer className="h-8 bg-slate-900 border-t border-slate-800 text-white px-4 flex items-center justify-between text-[11px] select-none">
      {/* États Direct et Enregistrement */}
      <div className="flex items-center space-x-6">
        <div className="flex items-center space-x-2">
          <span
            className={`w-2.5 h-2.5 rounded-full ${
              isLive ? "bg-live animate-pulse" : "bg-slate-600"
            }`}
          />
          <span className="font-bold text-slate-300">LIVE :</span>
          <span className={`font-mono ${isLive ? "text-live font-bold" : "text-slate-500"}`}>
            {isLive ? liveDuration : "00:00:00"}
          </span>
        </div>

        <div className="flex items-center space-x-2">
          <span
            className={`w-2.5 h-2.5 rounded-full ${
              isRecording ? "bg-red-500" : "bg-slate-600"
            }`}
          />
          <span className="font-bold text-slate-300">REC :</span>
          <span className={`font-mono ${isRecording ? "text-red-400 font-bold" : "text-slate-500"}`}>
            {isRecording ? recordDuration : "00:00:00"}
          </span>
        </div>
      </div>

      {/* Métriques Matérielles & Réseau */}
      <div className="flex items-center space-x-5 text-slate-400 font-mono">
        <div className="flex items-center space-x-1.5">
          <Cpu className="w-3.5 h-3.5 text-slate-400" />
          <span>CPU: {cpuUsage}%</span>
        </div>

        <div className="flex items-center space-x-1.5">
          <HardDrive className="w-3.5 h-3.5 text-slate-400" />
          <span>RAM: {memoryUsageMb} MB</span>
        </div>

        <div className="flex items-center space-x-1.5">
          <Activity className="w-3.5 h-3.5 text-slate-400" />
          <span>FPS: {fps}</span>
        </div>

        <div className="flex items-center space-x-1.5">
          <span className="text-slate-500">Pertes:</span>
          <span className={droppedFrames > 0 ? "text-amber-400" : "text-slate-400"}>
            {droppedFrames} (0.0%)
          </span>
        </div>

        <div className="flex items-center space-x-1.5">
          <Wifi className="w-3.5 h-3.5 text-green-400" />
          <span className="text-green-400 font-bold">{isLive ? bitrateKbps : 0} kb/s</span>
        </div>
      </div>
    </footer>
  );
};
