"use client";

import { useCallback, useEffect, useState } from "react";
import { apiClient } from "@/lib/api/client";

/**
 * Converts a Base64URL-encoded string to a Uint8Array.
 * Used to convert the VAPID public key into the format required by
 * PushManager.subscribe()'s applicationServerKey option.
 */
export function urlBase64ToUint8Array(base64String: string): Uint8Array {
  const padding = "=".repeat((4 - (base64String.length % 4)) % 4);
  const base64 = (base64String + padding).replace(/-/g, "+").replace(/_/g, "/");
  const rawData = atob(base64);
  const outputArray = new Uint8Array(rawData.length);
  for (let i = 0; i < rawData.length; i++) {
    outputArray[i] = rawData.charCodeAt(i);
  }
  return outputArray;
}

export interface UsePushSubscriptionReturn {
  /** Whether push is supported in this browser */
  isSupported: boolean;
  /** Current permission state: 'default' | 'granted' | 'denied' */
  permission: NotificationPermission;
  /** Whether the user has an active subscription for this space */
  isSubscribed: boolean;
  /** Loading state during subscribe/unsubscribe operations */
  isLoading: boolean;
  /** Subscribe to push notifications (requests permission if needed) */
  subscribe: () => Promise<void>;
  /** Unsubscribe from push notifications */
  unsubscribe: () => Promise<void>;
}

/**
 * React hook managing the full push subscription lifecycle.
 * Checks browser support, manages permission requests, and syncs
 * subscription state with the backend API.
 */
export function usePushSubscription(spaceId: string): UsePushSubscriptionReturn {
  const [isSupported, setIsSupported] = useState(false);
  const [permission, setPermission] = useState<NotificationPermission>("default");
  const [isSubscribed, setIsSubscribed] = useState(false);
  const [isLoading, setIsLoading] = useState(false);

  // Check browser support on mount
  useEffect(() => {
    const supported =
      typeof window !== "undefined" &&
      "serviceWorker" in navigator &&
      "PushManager" in window &&
      "Notification" in window;

    setIsSupported(supported);

    if (supported) {
      setPermission(Notification.permission);
    }
  }, []);

  // Check subscription status with backend on mount
  useEffect(() => {
    if (!isSupported || !spaceId) return;

    async function checkStatus() {
      try {
        const { data } = await apiClient.get<{ isSubscribed: boolean }>(
          `/spaces/${spaceId}/push-subscriptions/status`
        );
        setIsSubscribed(data.isSubscribed);
      } catch {
        // If status check fails, assume not subscribed
        setIsSubscribed(false);
      }
    }

    checkStatus();
  }, [isSupported, spaceId]);

  const subscribe = useCallback(async () => {
    if (!isSupported || isLoading) return;

    setIsLoading(true);
    try {
      // Step 1: Request notification permission
      const result = await Notification.requestPermission();
      setPermission(result);

      if (result !== "granted") {
        return;
      }

      // Step 2: Get service worker registration and subscribe via PushManager
      const registration = await navigator.serviceWorker.ready;
      const vapidPublicKey = process.env.NEXT_PUBLIC_VAPID_PUBLIC_KEY;

      if (!vapidPublicKey) {
        console.error("VAPID public key not configured");
        return;
      }

      const subscription = await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(vapidPublicKey),
      });

      // Step 3: Send subscription to backend
      const keys = subscription.toJSON().keys!;
      await apiClient.post(`/spaces/${spaceId}/push-subscriptions`, {
        endpoint: subscription.endpoint,
        p256dh: keys.p256dh,
        auth: keys.auth,
      });

      setIsSubscribed(true);
    } catch (error) {
      console.error("Failed to subscribe to push notifications:", error);
    } finally {
      setIsLoading(false);
    }
  }, [isSupported, isLoading, spaceId]);

  const unsubscribe = useCallback(async () => {
    if (!isSupported || isLoading) return;

    setIsLoading(true);
    try {
      // Step 1: Get current subscription
      const registration = await navigator.serviceWorker.ready;
      const subscription = await registration.pushManager.getSubscription();

      // Step 2: Delete from backend
      if (subscription) {
        await apiClient.delete(`/spaces/${spaceId}/push-subscriptions`, {
          data: { endpoint: subscription.endpoint },
        });

        // Step 3: Unsubscribe from PushManager
        await subscription.unsubscribe();
      }

      setIsSubscribed(false);
    } catch (error) {
      console.error("Failed to unsubscribe from push notifications:", error);
    } finally {
      setIsLoading(false);
    }
  }, [isSupported, isLoading, spaceId]);

  return {
    isSupported,
    permission,
    isSubscribed,
    isLoading,
    subscribe,
    unsubscribe,
  };
}
