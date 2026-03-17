export interface InventoryItem {
  gearItemId: number;
  name: string;
  gearType: string;
  quantity: number;
  flavorText?: string | null;
  costInCogs: number;
}

export interface UserProfile {
  userAccountId: number;
  displayName: string;
  email: string;
  avatarUrl?: string | null;
  cogBalance: number;
  roles: string[];
  inventory: InventoryItem[];
  isAdmin: boolean;
}

export interface StoreItem {
  gearItemId: number;
  name: string;
  description?: string | null;
  gearType: string;
  costInCogs: number;
  stockQuantity?: number | null;
  isActive: boolean;
  flavorText?: string | null;
}

export interface DashboardResponse {
  pilot: UserProfile;
  storeFront: StoreItem[];
}

export interface PurchaseReceipt {
  message: string;
  quantityPurchased: number;
  cogsSpent: number;
  newCogBalance: number;
  gear: InventoryItem;
}

export interface AdminUserSummary {
  userAccountId: number;
  displayName: string;
  email: string;
  cogBalance: number;
  roles: string[];
}

export interface GrantCogsPayload {
  userAccountId: number;
  amount: number;
  note: string;
}

export interface GrantGearPayload {
  userAccountId: number;
  gearItemId: number;
  quantity: number;
  note: string;
}

export interface UpsertGearPayload {
  name: string;
  description?: string | null;
  gearType: string;
  costInCogs: number;
  stockQuantity?: number | null;
  isActive: boolean;
  flavorText?: string | null;
}
