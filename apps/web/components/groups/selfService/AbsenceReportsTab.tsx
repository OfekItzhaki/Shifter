"use client";

import { useCallback, useEffect, useState } from "react";
import { useTranslations } from "next-intl";
import {
  AbsenceReportDto,
  approveAbsenceReport,
  getAbsenceReports,
  rejectAbsenceReport,
} from "@/lib/api/selfService";
import { getSelfServiceErrorMessage } from "@/lib/utils/selfServiceErrors";
import { formatSlotDate, formatTime24h } from "@/lib/utils/selfServiceFormat";
import LoadingCard from "./LoadingCard";
import ErrorRetry from "./ErrorRetry";
import MutationButton from "./MutationButton";

interface Props {
  spaceId: string;
  groupId: string;
}

const STATUS_STYLES: Record<AbsenceReportDto["status"], string> = {
  Pending: "border-amber-200 bg-amber-50 text-amber-700",
  Approved: "border-emerald-200 bg-emerald-50 text-emerald-700",
  Rejected: "border-red-200 bg-red-50 text-red-700",
};

export default function AbsenceReportsTab({ spaceId, groupId }: Props) {
  const t = useTranslations("selfService.absenceReports");
  const [reports, setReports] = useState<AbsenceReportDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [adminNotes, setAdminNotes] = useState<Record<string, string>>({});
  const [actionLoading, setActionLoading] = useState<Record<string, "approve" | "reject">>({});
  const [actionError, setActionError] = useState<string | null>(null);

  const fetchReports = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      setReports(await getAbsenceReports(spaceId, groupId));
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setError(message);
    } finally {
      setLoading(false);
    }
  }, [spaceId, groupId]);

  useEffect(() => {
    fetchReports();
  }, [fetchReports]);

  async function review(reportId: string, action: "approve" | "reject") {
    setActionLoading((prev) => ({ ...prev, [reportId]: action }));
    setActionError(null);

    try {
      const note = adminNotes[reportId] ?? "";
      if (action === "approve") {
        await approveAbsenceReport(spaceId, groupId, reportId, note);
      } else {
        await rejectAbsenceReport(spaceId, groupId, reportId, note);
      }
      await fetchReports();
    } catch (err) {
      const { message } = getSelfServiceErrorMessage(err);
      setActionError(message);
    } finally {
      setActionLoading((prev) => {
        const next = { ...prev };
        delete next[reportId];
        return next;
      });
    }
  }

  if (loading) return <LoadingCard rows={4} variant="list" />;
  if (error) return <ErrorRetry message={error} onRetry={fetchReports} />;

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold text-slate-700">{t("title")}</h2>
        <button
          type="button"
          onClick={fetchReports}
          className="rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-600 transition-colors hover:bg-slate-50"
        >
          {t("refresh")}
        </button>
      </div>

      {actionError && (
        <div className="rounded-lg border border-red-200 bg-red-50 p-3 text-xs text-red-700">
          {actionError}
        </div>
      )}

      {reports.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-xl border border-slate-200 bg-white py-12 text-center">
          <p className="text-sm text-slate-400">{t("empty")}</p>
        </div>
      ) : (
        <div className="space-y-3">
          {reports.map((report) => (
            <div key={report.id} className="rounded-xl border border-slate-200 bg-white p-4">
              <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="text-sm font-semibold text-slate-900">{report.personName}</p>
                    <span className={`rounded-full border px-2 py-0.5 text-xs font-medium ${STATUS_STYLES[report.status]}`}>
                      {t(`status${report.status}`)}
                    </span>
                    {report.isLate && (
                      <span className="rounded-full border border-orange-200 bg-orange-50 px-2 py-0.5 text-xs font-medium text-orange-700">
                        {t("late")}
                      </span>
                    )}
                  </div>
                  <p className="mt-1 text-xs text-slate-500">
                    {formatSlotDate(report.date)} · {formatTime24h(report.startTime)}-{formatTime24h(report.endTime)} · {report.taskName}
                  </p>
                  <p className="mt-2 text-sm text-slate-700">{report.reason}</p>
                  {report.adminNote && (
                    <p className="mt-2 text-xs text-slate-500">{t("adminNote")}: {report.adminNote}</p>
                  )}
                </div>

                {report.status === "Pending" && (
                  <div className="w-full shrink-0 space-y-2 sm:w-64">
                    <input
                      value={adminNotes[report.id] ?? ""}
                      onChange={(e) => setAdminNotes((prev) => ({ ...prev, [report.id]: e.target.value }))}
                      maxLength={500}
                      placeholder={t("adminNotePlaceholder")}
                      className="w-full rounded-lg border border-slate-200 px-3 py-2 text-xs focus:outline-none focus:ring-2 focus:ring-sky-400"
                    />
                    <div className="flex gap-2">
                      <MutationButton
                        onClick={() => review(report.id, "approve")}
                        loading={actionLoading[report.id] === "approve"}
                        disabled={!!actionLoading[report.id]}
                        label={t("approve")}
                        loadingLabel={t("approving")}
                        variant="primary"
                      />
                      <MutationButton
                        onClick={() => review(report.id, "reject")}
                        loading={actionLoading[report.id] === "reject"}
                        disabled={!!actionLoading[report.id]}
                        label={t("reject")}
                        loadingLabel={t("rejecting")}
                        variant="danger"
                      />
                    </div>
                  </div>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
