"use client";

import { useEffect, useState, useCallback, lazy, Suspense } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { useEffectiveAuth } from "@/lib/hooks/useEffectiveAuth";
import { getGroupMembers, getGroups, type GroupMemberDto, type GroupWithMemberCountDto } from "@/lib/api/groups";
import { filterSelfServiceGroups } from "@/lib/utils/pickGroupFilter";
import {
  getLastGroup,
  setLastGroup,
  clearLastGroup,
  resolveLastGroup,
} from "@/lib/utils/pickLastGroup";
import PickerHeader from "@/components/pick/PickerHeader";
import GroupSelector from "@/components/pick/GroupSelector";
import PickerTabs, { type PickerTab } from "@/components/pick/PickerTabs";
import LoadingCard from "@/components/groups/selfService/LoadingCard";
import ErrorRetry from "@/components/groups/selfService/ErrorRetry";

// Lazy-load tab content components for code splitting
const SlotBrowserTab = lazy(() => import("@/app/groups/[groupId]/tabs/SlotBrowserTab"));
const MyShiftsTab = lazy(() => import("@/components/groups/selfService/MyShiftsTab"));
const WaitlistTab = lazy(() => import("@/app/groups/[groupId]/tabs/WaitlistTab"));
const SwapsTab = lazy(() => import("@/app/groups/[groupId]/tabs/SwapsTab"));

type Phase = "loading" | "group-select" | "slot-browser";

/**
 * /pick route page — lightweight shift picker for members.
 * Renders outside the main app shell (no sidebar).
 * Implements a state machine: loading → group-select | slot-browser.
 *
 * Validates: Requirements 1.1, 1.3, 1.4, 3.1, 3.2, 3.5
 */
export default function PickPage() {
  const router = useRouter();
  const t = useTranslations("pick");
  const { isLoggedIn, isHydrated } = useEffectiveAuth();
  const { currentSpaceId } = useSpaceStore();

  const [phase, setPhase] = useState<Phase>("loading");
  const [selfServiceGroups, setSelfServiceGroups] = useState<GroupWithMemberCountDto[]>([]);
  const [selectedGroupId, setSelectedGroupId] = useState<string | null>(null);
  const [selectedGroupName, setSelectedGroupName] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<PickerTab>("slots");
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [refreshKey, setRefreshKey] = useState(0);
  const [members, setMembers] = useState<GroupMemberDto[]>([]);
  const [membersLoading, setMembersLoading] = useState(false);
  const [membersError, setMembersError] = useState<string | null>(null);

  // Auth guard: redirect to login if not authenticated
  useEffect(() => {
    if (isHydrated && !isLoggedIn) {
      router.replace("/login?redirect=/pick");
    }
  }, [isHydrated, isLoggedIn, router]);

  // Load groups and resolve last-group on mount
  const loadGroups = useCallback(async () => {
    if (!currentSpaceId) return;

    setError(null);
    try {
      const allGroups = await getGroups(currentSpaceId);
      const filtered = filterSelfServiceGroups(allGroups);
      setSelfServiceGroups(filtered);

      // Resolve last-group memory
      const storedGroupId = getLastGroup();
      const validGroupId = resolveLastGroup(storedGroupId, filtered);

      if (validGroupId) {
        const group = filtered.find((g) => g.id === validGroupId);
        setSelectedGroupId(validGroupId);
        setSelectedGroupName(group?.name ?? null);
        setMembers([]);
        setMembersError(null);
        setPhase("slot-browser");
      } else {
        clearLastGroup();
        setPhase("group-select");
      }
    } catch {
      setError(t("error"));
      setPhase("group-select");
    }
  }, [currentSpaceId, t]);

  useEffect(() => {
    if (isLoggedIn && currentSpaceId) {
      loadGroups();
    }
  }, [isLoggedIn, currentSpaceId, loadGroups]);

  // Handle group selection from GroupSelector
  const handleGroupSelect = useCallback((groupId: string, groupName: string) => {
    setLastGroup(groupId);
    setSelectedGroupId(groupId);
    setSelectedGroupName(groupName);
    setMembers([]);
    setMembersError(null);
    setActiveTab("slots");
    setPhase("slot-browser");
  }, []);

  // Handle back navigation to group selector
  const handleBack = useCallback(() => {
    setPhase("group-select");
  }, []);

  // Handle refresh — triggers data reload in active tab via key change
  const handleRefresh = useCallback(async () => {
    setRefreshing(true);
    setRefreshKey((k) => k + 1);
    if (activeTab === "swaps") {
      setMembers([]);
      setMembersError(null);
    }
    // Small delay to show the spinner
    setTimeout(() => setRefreshing(false), 600);
  }, [activeTab]);

  // Handle tab change
  const handleTabChange = useCallback((tab: PickerTab) => {
    setActiveTab(tab);
  }, []);

  const loadMembers = useCallback(async () => {
    if (!currentSpaceId || !selectedGroupId) return;

    setMembersLoading(true);
    setMembersError(null);
    try {
      setMembers(await getGroupMembers(currentSpaceId, selectedGroupId));
    } catch {
      setMembersError(t("error"));
    } finally {
      setMembersLoading(false);
    }
  }, [currentSpaceId, selectedGroupId, t]);

  useEffect(() => {
    if (activeTab === "swaps" && selectedGroupId && currentSpaceId && members.length === 0 && !membersLoading) {
      void Promise.resolve().then(loadMembers);
    }
  }, [activeTab, currentSpaceId, loadMembers, members.length, membersLoading, selectedGroupId]);

  // Don't render anything if not authenticated (redirect in progress)
  if (!isHydrated || !isLoggedIn) {
    return null;
  }

  return (
    <div className="min-h-dvh bg-slate-50 dark:bg-slate-900 flex flex-col">
      <PickerHeader
        groupName={phase === "slot-browser" ? selectedGroupName : null}
        onBack={handleBack}
        onRefresh={handleRefresh}
        refreshing={refreshing}
      />

      <main className="flex-1 w-full max-w-lg mx-auto px-4 py-4 space-y-4">
        {/* Loading phase */}
        {phase === "loading" && (
          <LoadingCard rows={4} variant="list" />
        )}

        {/* Error state */}
        {error && phase === "group-select" && (
          <ErrorRetry message={error} onRetry={loadGroups} />
        )}

        {/* Group selection phase */}
        {phase === "group-select" && !error && (
          <div className="space-y-4">
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">
              {t("selectGroup")}
            </h2>
            <GroupSelector
              groups={selfServiceGroups}
              onSelect={handleGroupSelect}
            />
          </div>
        )}

        {/* Slot browser phase */}
        {phase === "slot-browser" && selectedGroupId && currentSpaceId && (
          <div className="space-y-4">
            <PickerTabs activeTab={activeTab} onTabChange={handleTabChange} />

            <div
              id={`panel-${activeTab}`}
              role="tabpanel"
              aria-labelledby={`tab-${activeTab}`}
            >
              <Suspense fallback={<LoadingCard rows={4} variant="slots" />}>
                {activeTab === "slots" && (
                  <SlotBrowserTab
                    key={`slots-${refreshKey}`}
                    spaceId={currentSpaceId}
                    groupId={selectedGroupId}
                    isAdmin={false}
                  />
                )}
                {activeTab === "my-shifts" && (
                  <MyShiftsTab
                    key={`my-shifts-${refreshKey}`}
                    spaceId={currentSpaceId}
                    groupId={selectedGroupId}
                  />
                )}
                {activeTab === "waitlist" && (
                  <WaitlistTab
                    key={`waitlist-${refreshKey}`}
                    spaceId={currentSpaceId}
                    groupId={selectedGroupId}
                  />
                )}
                {activeTab === "swaps" && (
                  membersLoading ? (
                    <LoadingCard rows={3} variant="list" />
                  ) : membersError ? (
                    <ErrorRetry message={membersError} onRetry={loadMembers} />
                  ) : (
                    <SwapsTab
                      key={`swaps-${refreshKey}`}
                      spaceId={currentSpaceId}
                      groupId={selectedGroupId}
                      members={members}
                    />
                  )
                )}
              </Suspense>
            </div>
          </div>
        )}
      </main>
    </div>
  );
}
