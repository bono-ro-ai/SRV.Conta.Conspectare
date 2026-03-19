import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router";
import type { UploadAcceptedResponse } from "../types/api";
import { uploadDocumentWithProgress } from "../services/api/documents";

interface DocumentUploadZoneProps {
  onUploadComplete?: (response: UploadAcceptedResponse) => void;
}

type UploadState = "idle" | "uploading" | "success" | "error";

const ALLOWED_MIME_TYPES = [
  "application/pdf",
  "image/png",
  "image/jpeg",
  "text/xml",
  "application/xml",
];

const ALLOWED_EXTENSIONS = [".pdf", ".png", ".jpg", ".jpeg", ".xml"];

const MAX_FILE_SIZE_BYTES = 10 * 1024 * 1024;

function getFileExtension(name: string): string {
  const dotIndex = name.lastIndexOf(".");
  if (dotIndex === -1) return "";
  return name.slice(dotIndex).toLowerCase();
}

function validateFile(file: File): string | null {
  if (file.size === 0) {
    return "File is empty.";
  }
  if (file.size > MAX_FILE_SIZE_BYTES) {
    return `File exceeds the 10 MB limit (${(file.size / 1024 / 1024).toFixed(1)} MB).`;
  }
  const ext = getFileExtension(file.name);
  if (!ALLOWED_EXTENSIONS.includes(ext)) {
    return `File type "${ext || "unknown"}" is not supported. Allowed: ${ALLOWED_EXTENSIONS.join(", ")}`;
  }
  if (!ALLOWED_MIME_TYPES.includes(file.type)) {
    return `MIME type "${file.type || "unknown"}" is not supported.`;
  }
  return null;
}

export function DocumentUploadZone({ onUploadComplete }: DocumentUploadZoneProps) {
  const [file, setFile] = useState<File | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [uploadState, setUploadState] = useState<UploadState>("idle");
  const [progress, setProgress] = useState(0);
  const [result, setResult] = useState<UploadAcceptedResponse | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [clientReference, setClientReference] = useState("");
  const fileInputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();

  useEffect(() => {
    if (uploadState !== "success") return;
    const timer = setTimeout(() => navigate("/"), 2000);
    return () => clearTimeout(timer);
  }, [uploadState, navigate]);

  const handleSelectFile = useCallback((selectedFile: File) => {
    const validationError = validateFile(selectedFile);
    if (validationError) {
      setErrorMessage(validationError);
      setUploadState("error");
      setFile(null);
      return;
    }
    setFile(selectedFile);
    setErrorMessage(null);
    setUploadState("idle");
    setResult(null);
  }, []);

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (e.dataTransfer.types.includes("Files")) {
      setIsDragging(true);
    }
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
  }, []);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
    if (!e.dataTransfer.types.includes("Files")) return;
    const droppedFile = e.dataTransfer.files[0];
    if (droppedFile) {
      handleSelectFile(droppedFile);
    }
  }, [handleSelectFile]);

  const handleFileInputChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const selected = e.target.files?.[0];
    if (selected) {
      handleSelectFile(selected);
    }
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  }, [handleSelectFile]);

  const handleBrowseClick = useCallback(() => {
    fileInputRef.current?.click();
  }, []);

  const handleUpload = useCallback(async () => {
    if (!file) return;
    setUploadState("uploading");
    setProgress(0);
    setErrorMessage(null);
    try {
      const response = await uploadDocumentWithProgress(
        file,
        clientReference || undefined,
        (pct) => setProgress(pct),
      );
      setUploadState("success");
      setResult(response);
      setProgress(100);
      onUploadComplete?.(response);
    } catch (err) {
      setUploadState("error");
      setErrorMessage(err instanceof Error ? err.message : "Upload failed.");
    }
  }, [file, clientReference, onUploadComplete]);

  const handleReset = useCallback(() => {
    setFile(null);
    setUploadState("idle");
    setProgress(0);
    setResult(null);
    setErrorMessage(null);
    setClientReference("");
  }, []);

  if (uploadState === "success" && result) {
    return (
      <div className="rounded-lg border border-green-200 bg-green-50 p-6 text-center">
        <div className="text-3xl">&#x2705;</div>
        <p className="mt-2 text-sm font-medium text-green-800">Document uploaded successfully</p>
        <div className="mt-3 space-y-1 text-sm text-green-700">
          <p>Document ID: <span className="font-mono font-semibold">{result.id}</span></p>
          <p>Status: <span className="font-semibold">{result.status}</span></p>
        </div>
        <button
          type="button"
          onClick={handleReset}
          className="mt-4 rounded-md bg-green-600 px-4 py-2 text-sm font-medium text-white hover:bg-green-700"
        >
          Upload another
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        onClick={handleBrowseClick}
        onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") handleBrowseClick(); }}
        role="button"
        tabIndex={0}
        className={`flex cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed p-8 transition-colors ${
          isDragging
            ? "border-blue-500 bg-blue-50"
            : "border-gray-300 bg-gray-50 hover:border-gray-400 hover:bg-gray-100"
        }`}
      >
        <div className="text-4xl text-gray-400">{"\u{1F4C2}"}</div>
        <p className="mt-2 text-sm font-medium text-gray-700">
          {file ? file.name : "Drag & drop a file here, or click to browse"}
        </p>
        {file && (
          <p className="mt-1 text-xs text-gray-500">
            {(file.size / 1024).toFixed(1)} KB
          </p>
        )}
        <p className="mt-1 text-xs text-gray-400">
          PDF, PNG, JPG, XML &mdash; max 10 MB
        </p>
        <input
          ref={fileInputRef}
          type="file"
          accept=".pdf,.png,.jpg,.jpeg,.xml"
          onChange={handleFileInputChange}
          className="hidden"
        />
      </div>
      <div>
        <label htmlFor="clientReference" className="block text-sm font-medium text-gray-700">
          Client Reference <span className="text-gray-400">(optional)</span>
        </label>
        <input
          id="clientReference"
          type="text"
          value={clientReference}
          onChange={(e) => setClientReference(e.target.value)}
          placeholder="e.g. INV-2026-001"
          className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm shadow-sm focus:border-blue-500 focus:ring-1 focus:ring-blue-500 focus:outline-none"
        />
      </div>
      {uploadState === "error" && errorMessage && (
        <div className="rounded-md border border-red-200 bg-red-50 p-3">
          <p className="text-sm text-red-700">{errorMessage}</p>
        </div>
      )}
      {uploadState === "uploading" && (
        <div className="space-y-1">
          <div className="flex justify-between text-xs text-gray-600">
            <span>Uploading...</span>
            <span>{progress}%</span>
          </div>
          <div className="h-2 w-full overflow-hidden rounded-full bg-gray-200">
            <div
              className="h-full rounded-full bg-blue-600 transition-all"
              style={{ width: `${progress}%` }}
            />
          </div>
        </div>
      )}
      <button
        type="button"
        onClick={handleUpload}
        disabled={!file || uploadState === "uploading"}
        className="w-full rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50"
      >
        {uploadState === "uploading" ? "Uploading..." : "Upload"}
      </button>
    </div>
  );
}
