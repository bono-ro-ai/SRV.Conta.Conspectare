import { useMemo } from "react";

interface CanonicalDataTableProps {
  canonicalOutputJson: string | null;
}

interface LineItem {
  description?: string;
  quantity?: number;
  unitPrice?: number;
  amount?: number;
  vatRate?: number;
  vatAmount?: number;
  unit?: string;
}

interface CanonicalData {
  supplier?: {
    name?: string;
    vatNumber?: string;
    regNumber?: string;
    address?: string;
    bankAccount?: string;
    bankName?: string;
  };
  customer?: {
    name?: string;
    vatNumber?: string;
    regNumber?: string;
    address?: string;
    bankAccount?: string;
    bankName?: string;
  };
  invoiceNumber?: string;
  invoiceDate?: string;
  dueDate?: string;
  currency?: string;
  lineItems?: LineItem[];
  subtotal?: number;
  totalVat?: number;
  totalAmount?: number;
}

function formatNumber(value: number | undefined | null): string {
  if (value === undefined || value === null) return "-";
  return new Intl.NumberFormat("ro-RO", { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
}

function PartySection({ title, party }: { title: string; party?: CanonicalData["supplier"] }) {
  if (!party) return null;
  return (
    <div>
      <h4 className="text-xs font-semibold uppercase tracking-wide text-gray-500">{title}</h4>
      <div className="mt-1 space-y-0.5 text-sm text-gray-800">
        {party.name && <p className="font-medium">{party.name}</p>}
        {party.vatNumber && <p>VAT: {party.vatNumber}</p>}
        {party.regNumber && <p>Reg: {party.regNumber}</p>}
        {party.address && <p>{party.address}</p>}
        {party.bankAccount && <p>IBAN: {party.bankAccount}</p>}
        {party.bankName && <p>Bank: {party.bankName}</p>}
      </div>
    </div>
  );
}

export function CanonicalDataTable({ canonicalOutputJson }: CanonicalDataTableProps) {
  const parsed = useMemo<CanonicalData | null>(() => {
    if (!canonicalOutputJson) return null;
    try {
      return JSON.parse(canonicalOutputJson) as CanonicalData;
    } catch {
      return null;
    }
  }, [canonicalOutputJson]);

  if (!canonicalOutputJson) {
    return (
      <div className="rounded-lg border border-gray-200 bg-gray-50 p-6 text-center">
        <p className="text-sm text-gray-500">No extraction data yet.</p>
      </div>
    );
  }

  if (!parsed) {
    return (
      <div className="rounded-lg border border-amber-200 bg-amber-50 p-6 text-center">
        <p className="text-sm text-amber-700">Invalid data format.</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-4">
        <PartySection title="Supplier" party={parsed.supplier} />
        <PartySection title="Customer" party={parsed.customer} />
      </div>
      <div className="flex flex-wrap gap-4 text-sm text-gray-700">
        {parsed.invoiceNumber && (
          <div>
            <span className="text-xs font-semibold uppercase tracking-wide text-gray-500">Invoice #</span>
            <p className="font-medium">{parsed.invoiceNumber}</p>
          </div>
        )}
        {parsed.invoiceDate && (
          <div>
            <span className="text-xs font-semibold uppercase tracking-wide text-gray-500">Date</span>
            <p>{parsed.invoiceDate}</p>
          </div>
        )}
        {parsed.dueDate && (
          <div>
            <span className="text-xs font-semibold uppercase tracking-wide text-gray-500">Due</span>
            <p>{parsed.dueDate}</p>
          </div>
        )}
        {parsed.currency && (
          <div>
            <span className="text-xs font-semibold uppercase tracking-wide text-gray-500">Currency</span>
            <p>{parsed.currency}</p>
          </div>
        )}
      </div>
      {parsed.lineItems && parsed.lineItems.length > 0 && (
        <div className="overflow-x-auto">
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wide text-gray-500">#</th>
                <th className="px-3 py-2 text-left text-xs font-medium uppercase tracking-wide text-gray-500">Description</th>
                <th className="px-3 py-2 text-right text-xs font-medium uppercase tracking-wide text-gray-500">Qty</th>
                <th className="px-3 py-2 text-right text-xs font-medium uppercase tracking-wide text-gray-500">Unit Price</th>
                <th className="px-3 py-2 text-right text-xs font-medium uppercase tracking-wide text-gray-500">VAT %</th>
                <th className="px-3 py-2 text-right text-xs font-medium uppercase tracking-wide text-gray-500">Amount</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {parsed.lineItems.map((item, idx) => (
                <tr key={idx}>
                  <td className="whitespace-nowrap px-3 py-2 text-gray-500">{idx + 1}</td>
                  <td className="px-3 py-2 text-gray-800">{item.description ?? "-"}</td>
                  <td className="whitespace-nowrap px-3 py-2 text-right text-gray-700">
                    {item.quantity != null ? `${item.quantity} ${item.unit ?? ""}`.trim() : "-"}
                  </td>
                  <td className="whitespace-nowrap px-3 py-2 text-right text-gray-700">{formatNumber(item.unitPrice)}</td>
                  <td className="whitespace-nowrap px-3 py-2 text-right text-gray-700">{item.vatRate != null ? `${item.vatRate}%` : "-"}</td>
                  <td className="whitespace-nowrap px-3 py-2 text-right font-medium text-gray-800">{formatNumber(item.amount)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      <div className="flex justify-end">
        <div className="space-y-1 text-sm">
          {parsed.subtotal != null && (
            <div className="flex justify-between gap-8">
              <span className="text-gray-500">Subtotal</span>
              <span className="font-medium text-gray-800">{formatNumber(parsed.subtotal)}</span>
            </div>
          )}
          {parsed.totalVat != null && (
            <div className="flex justify-between gap-8">
              <span className="text-gray-500">VAT</span>
              <span className="font-medium text-gray-800">{formatNumber(parsed.totalVat)}</span>
            </div>
          )}
          {parsed.totalAmount != null && (
            <div className="flex justify-between gap-8 border-t border-gray-200 pt-1">
              <span className="font-semibold text-gray-700">Total</span>
              <span className="font-bold text-gray-900">{formatNumber(parsed.totalAmount)}</span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
