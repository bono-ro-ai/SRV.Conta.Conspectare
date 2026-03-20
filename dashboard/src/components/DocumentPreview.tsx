import { useCallback, useEffect, useRef, useState } from "react";
import { authFetch } from "../services/api/authFetch";

interface DocumentPreviewProps {
  contentType: string;
  documentId: number;
}

export function DocumentPreview({ contentType, documentId }: DocumentPreviewProps) {
  const [blobUrl, setBlobUrl] = useState<string | null>(null);
  const [textContent, setTextContent] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const controllerRef = useRef<AbortController | null>(null);

  const fetchContent = useCallback(async () => {
    controllerRef.current?.abort();
    const controller = new AbortController();
    controllerRef.current = controller;
    setLoading(true);
    setError(null);
    try {
      const response = await authFetch(`/api/v1/documents/${documentId}/raw`, {
        signal: controller.signal,
      });
      if (controller.signal.aborted) return;
      if (contentType.startsWith("image/")) {
        const blob = await response.blob();
        if (!controller.signal.aborted) setBlobUrl(URL.createObjectURL(blob));
      } else if (
        contentType === "text/xml" ||
        contentType === "application/xml" ||
        contentType.startsWith("text/")
      ) {
        const text = await response.text();
        if (!controller.signal.aborted) setTextContent(text);
      } else if (contentType === "application/pdf") {
        const blob = await response.blob();
        if (!controller.signal.aborted) setBlobUrl(URL.createObjectURL(blob));
      }
    } catch (err) {
      if (controller.signal.aborted) return;
      setError(err instanceof Error ? err.message : "Failed to load preview.");
    } finally {
      if (!controller.signal.aborted) setLoading(false);
    }
  }, [contentType, documentId]);

  useEffect(() => {
    fetchContent();
    return () => {
      controllerRef.current?.abort();
      if (blobUrl) {
        URL.revokeObjectURL(blobUrl);
      }
    };
  }, [fetchContent]); // eslint-disable-line react-hooks/exhaustive-deps

  if (loading) {
    return (
      <div className="flex items-center justify-center rounded-lg border border-gray-200 bg-gray-50 p-12">
        <span className="text-sm text-gray-500">Loading preview...</span>
      </div>
    );
  }

  if (error) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6">
        <p className="text-sm text-red-600">{error}</p>
      </div>
    );
  }

  if (contentType === "application/pdf" && blobUrl) {
    return (
      <iframe
        src={blobUrl}
        className="h-[600px] w-full rounded-lg border border-gray-200"
        title="Document preview"
      />
    );
  }

  if (contentType.startsWith("image/") && blobUrl) {
    return (
      <img
        src={blobUrl}
        alt="Document preview"
        className="max-h-[600px] w-full rounded-lg border border-gray-200 object-contain"
      />
    );
  }

  if (textContent !== null) {
    return (
      <pre className="max-h-[600px] overflow-auto rounded-lg border border-gray-200 bg-gray-50 p-4 text-xs leading-relaxed">
        {textContent}
      </pre>
    );
  }

  return (
    <div className="flex flex-col items-center justify-center gap-2 rounded-lg border border-gray-200 bg-gray-50 p-12">
      <p className="text-sm text-gray-500">Preview not available for this file type.</p>
      <button
        onClick={fetchContent}
        className="text-sm font-medium text-indigo-600 hover:text-indigo-500"
      >
        Download raw file
      </button>
    </div>
  );
}
