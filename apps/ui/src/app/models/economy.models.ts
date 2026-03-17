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

export interface CraftGearPayload {
  name: string;
  description?: string | null;
  gearType: string;
  craftingCostInCogs: number;
  flavorText?: string | null;
}

export interface CraftGearReceipt {
  message: string;
  cogsSpent: number;
  newCogBalance: number;
  gear: InventoryItem;
}

export interface MarketplaceListing {
  marketplaceListingId: number;
  gearItemId: number;
  gearName: string;
  gearType: string;
  flavorText?: string | null;
  quantity: number;
  priceInCogs: number;
  sellerUserAccountId: number;
  sellerDisplayName: string;
  listingStatus: string;
  sellerNote?: string | null;
  createdAtUtc: string;
  soldAtUtc?: string | null;
}

export interface CreateMarketplaceListingPayload {
  gearItemId: number;
  quantity: number;
  priceInCogs: number;
  sellerNote?: string | null;
}

export interface MarketplacePurchaseReceipt {
  message: string;
  cogsSpent: number;
  newCogBalance: number;
  gear: InventoryItem;
}

export interface CogSession {
  cogSessionId: number;
  cogInAtUtc: string;
  cogOutAtUtc?: string | null;
  durationMinutes?: number | null;
  isOpen: boolean;
  cogInNote?: string | null;
  cogOutNote?: string | null;
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
