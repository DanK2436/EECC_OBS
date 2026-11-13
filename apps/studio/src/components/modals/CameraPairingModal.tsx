import React from "react";
import { QRCodeSVG } from "qrcode.react";
import { X, Smartphone } from "lucide-react";

interface CameraPairingModalProps {
  isOpen: boolean;
  onClose: () => void;
  localIp: string;
  port: number;
}

export const CameraPairingModal: React.FC<CameraPairingModalProps> = ({
  isOpen,
  onClose,
  localIp,
  port,
}) => {
  if (!isOpen) return null;

  const addressKnown = Boolean(localIp) && !/non détect/i.test(localIp);
  const pairingPayload = JSON.stringify({
    studio_ip: addressKnown ? localIp : "",
    port: port,
    token: `sess_${Date.now()}`,
    stream_name: "Caméra Smartphone 1",
    codec: "h264",
    bitrate_kbps: 4500,
  });

  return (
    <div className="sheet-back">
      <div className="sheet narrow" role="dialog" aria-labelledby="pair-title">
        <header className="sheet-head">
          <span id="pair-title" style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
            <Smartphone size={14} /> Appairer une caméra
          </span>
          <button type="button" className="sheet-x" onClick={onClose} aria-label="Fermer">
            <X size={14} />
          </button>
        </header>
        <div className="sheet-body" style={{ textAlign: "center" }}>
          <div style={{ marginBottom: 8 }}>
            <span className={`sheet-badge ${addressKnown ? "" : "warn"}`}>
              {addressKnown ? `${localIp}:${port}` : "Adresse non détectée"}
            </span>
          </div>
          <div className="qr-plate">
            <QRCodeSVG value={pairingPayload} size={180} level="H" fgColor="#111111" bgColor="#FFFFFF" includeMargin />
          </div>
          <p style={{ color: "#c5ccd3", margin: "10px auto 0", maxWidth: 320, lineHeight: 1.4 }}>
            Ouvrez EECC Caméra et scannez ce code. Le QR reste clair pour rester lisible. Aucune caméra n'est connectée tant qu'un appareil ne répond pas.
          </p>
          <div className="sheet-kv" style={{ marginTop: 12 }}>
            <span>Protocole</span>
            <b>SRT / WebRTC LAN</b>
          </div>
          <div className="sheet-kv">
            <span>État</span>
            <span className="sheet-badge">En attente</span>
          </div>
        </div>
        <footer className="sheet-foot">
          <button type="button" className="sheet-btn" onClick={onClose}>Fermer</button>
        </footer>
      </div>
    </div>
  );
};
