import React from "react";
import { QRCodeSVG } from "qrcode.react";
import { X, Smartphone, Wifi, CheckCircle2 } from "lucide-react";

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

  const pairingPayload = JSON.stringify({
    studio_ip: localIp,
    port: port,
    token: `sess_${Date.now()}`,
    stream_name: "Caméra Smartphone 1",
    codec: "h264",
    bitrate_kbps: 4500,
  });

  return (
    <div className="fixed inset-0 z-50 bg-black/60 backdrop-blur-xs flex items-center justify-center p-4">
      <div className="bg-white rounded-2xl w-full max-w-md shadow-2xl border border-slate-200 overflow-hidden">
        {/* Header */}
        <div className="px-6 py-4 bg-slate-900 text-white flex items-center justify-between">
          <div className="flex items-center space-x-2">
            <Smartphone className="w-5 h-5 text-blue-400" />
            <h3 className="font-bold text-sm">Appairer une Caméra Mobile</h3>
          </div>
          <button
            onClick={onClose}
            className="p-1 rounded hover:bg-slate-800 text-slate-400 hover:text-white"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Corps */}
        <div className="p-6 text-center">
          <div className="inline-flex items-center px-3 py-1 rounded-full text-xs font-semibold bg-green-50 text-green-700 border border-green-200 mb-4">
            <Wifi className="w-3.5 h-3.5 mr-1.5" />
            Serveur local prêt : {localIp}:{port}
          </div>

          {/* QR Code */}
          <div className="bg-slate-50 p-6 rounded-2xl border border-slate-200 inline-block shadow-inner mb-4">
            <QRCodeSVG value={pairingPayload} size={200} level="H" />
          </div>

          <p className="text-xs text-slate-600 max-w-xs mx-auto leading-relaxed">
            Ouvrez l’application mobile <strong>EECC Caméra</strong> sur votre téléphone (Android / iOS) et scannez ce code pour démarrer la diffusion directe vers Studio.
          </p>

          <div className="mt-6 pt-4 border-t border-slate-100 flex items-center justify-between text-xs text-slate-400">
            <span>Protocole : SRT / WebRTC LAN</span>
            <span className="flex items-center text-green-600 font-semibold">
              <CheckCircle2 className="w-3.5 h-3.5 mr-1" /> En attente de connexion
            </span>
          </div>
        </div>
      </div>
    </div>
  );
};
