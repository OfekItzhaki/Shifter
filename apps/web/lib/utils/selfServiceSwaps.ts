import type { SwapRequestDto } from "@/lib/api/selfService";

export interface ClassifiedSwaps {
  incomingSwaps: SwapRequestDto[];
  outgoingSwaps: SwapRequestDto[];
  completedSwaps: SwapRequestDto[];
}

export function classifySwapsForPerson(
  swaps: SwapRequestDto[],
  personId: string | null
): ClassifiedSwaps {
  return {
    incomingSwaps: swaps.filter(
      (swap) => swap.targetPersonId === personId && swap.status === "Pending"
    ),
    outgoingSwaps: swaps.filter(
      (swap) => swap.initiatorPersonId === personId && swap.status === "Pending"
    ),
    completedSwaps: swaps.filter((swap) => swap.status !== "Pending"),
  };
}
