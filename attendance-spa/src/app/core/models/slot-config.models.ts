export interface SlotConfigDto {
  slotId: number;
  slotName: string;
  startTime: string;
  endTime: string;
  gracePeriodMinutes: number;
  isMandatory: boolean;
  isActive: boolean;
}

export interface CreateSlotRequest {
  slotName: string;
  startTime: string;
  endTime: string;
  gracePeriodMinutes: number;
  isMandatory: boolean;
}

export type UpdateSlotRequest = CreateSlotRequest;

export const SLOT_NAMES = ['MorningIn', 'LunchOut', 'LunchIn', 'EveningOut'] as const;
