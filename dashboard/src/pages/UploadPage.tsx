import { DocumentUploadZone } from "../components/DocumentUploadZone";

export function UploadPage() {
  return (
    <div>
      <h2 className="text-lg font-medium text-gray-900">Upload Document</h2>
      <div className="mx-auto mt-4 max-w-lg rounded-lg border border-gray-200 bg-white p-6 shadow-sm">
        <DocumentUploadZone />
      </div>
    </div>
  );
}
