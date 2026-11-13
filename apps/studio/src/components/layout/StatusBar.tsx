import React from "react";
import { Link2, MonitorSmartphone, Volume2 } from "lucide-react";
import type { StatusView } from "../../lib/studioMetrics";

interface StatusBarProps {
  status: StatusView;
  masterVolume: number;
  onMasterVolume: (value: number) => void;
}

export const StatusBar: React.FC<StatusBarProps> = ({ status, masterVolume, onMasterVolume }) => {
  return (
    <footer className="statusbar">
      <div className="status-left">
        {status.exampleData && <span className="demo-chip">MODE DÉMO</span>}
        <span className="stat">
          <i className={`dot ${status.liveOn ? "live" : ""}`} />
          <b>LIVE</b>
          <span className="mono">{status.liveTime}</span>
        </span>
        <span className="stat">
          <i className={`dot ${status.recOn ? "rec" : "rec"}`} />
          <b>REC</b>
          <span className="mono">{status.recTime}</span>
        </span>
        <i className="sep" />
        <span className="stat">
          CPU : <span className="mono">{status.cpu}</span>
        </span>
        <i className="sep" />
        <span className={`fps ${status.liveOn || status.exampleData ? "hot" : ""}`}>{status.fps}</span>
        <i className="sep" />
        <span className="stat">
          <Link2 size={12} />
          Débit : <span className="mono">{status.bitrate}</span>
        </span>
        <span className="stat">
          <MonitorSmartphone size={12} />
          LAN : <span className="mono">{status.lan}</span>
        </span>
      </div>
      <div className="status-right">
        <Volume2 size={13} />
        <input
          className="vol"
          type="range"
          min={0}
          max={100}
          step={0.1}
          value={masterVolume}
          aria-label="Volume master"
          onChange={(e) => onMasterVolume(Number(e.target.value))}
        />
      </div>
    </footer>
  );
};
