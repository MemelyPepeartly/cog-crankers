import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { appSettings } from '../app.settings';
import {
  AdminUserSummary,
  CogCheckStatus,
  CogRuntimeSettings,
  CogSession,
  CraftGearPayload,
  CraftGearReceipt,
  CreateMarketplaceListingPayload,
  DashboardResponse,
  GrantCogsPayload,
  GrantGearPayload,
  InventoryItem,
  MarketplaceListing,
  MarketplacePurchaseReceipt,
  PurchaseReceipt,
  StoreItem,
  UpdateDisplayNamePayload,
  UpdateCogWarningIntervalPayload,
  UpsertGearPayload,
  UserProfile
} from '../models/economy.models';

@Injectable({
  providedIn: 'root'
})
export class EconomyApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = appSettings.apiBaseUrl;

  startGoogleLogin(): void {
    window.location.assign(`${this.baseUrl}/api/auth/login`);
  }

  logout(): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.baseUrl}/api/auth/logout`, {}, {
      withCredentials: true
    });
  }

  getCurrentUserProfile(): Observable<UserProfile> {
    return this.http.get<UserProfile>(`${this.baseUrl}/api/auth/me`, {
      withCredentials: true
    });
  }

  updateDisplayName(payload: UpdateDisplayNamePayload): Observable<UserProfile> {
    return this.http.put<UserProfile>(`${this.baseUrl}/api/auth/display-name`, payload, {
      withCredentials: true
    });
  }

  getDashboard(): Observable<DashboardResponse> {
    return this.http.get<DashboardResponse>(`${this.baseUrl}/api/economy/dashboard`, {
      withCredentials: true
    });
  }

  buyGear(gearItemId: number, quantity: number): Observable<PurchaseReceipt> {
    return this.http.post<PurchaseReceipt>(`${this.baseUrl}/api/economy/store/${gearItemId}/buy`, {
      quantity
    }, {
      withCredentials: true
    });
  }

  craftGear(payload: CraftGearPayload): Observable<CraftGearReceipt> {
    return this.http.post<CraftGearReceipt>(`${this.baseUrl}/api/economy/craft`, payload, {
      withCredentials: true
    });
  }

  getMarketplaceListings(): Observable<MarketplaceListing[]> {
    return this.http.get<MarketplaceListing[]>(`${this.baseUrl}/api/economy/marketplace/listings`, {
      withCredentials: true
    });
  }

  createMarketplaceListing(payload: CreateMarketplaceListingPayload): Observable<MarketplaceListing> {
    return this.http.post<MarketplaceListing>(`${this.baseUrl}/api/economy/marketplace/listings`, payload, {
      withCredentials: true
    });
  }

  buyMarketplaceListing(marketplaceListingId: number): Observable<MarketplacePurchaseReceipt> {
    return this.http.post<MarketplacePurchaseReceipt>(
      `${this.baseUrl}/api/economy/marketplace/listings/${marketplaceListingId}/buy`,
      {},
      {
        withCredentials: true
      }
    );
  }

  cogIn(note?: string | null): Observable<CogSession> {
    return this.http.post<CogSession>(`${this.baseUrl}/api/economy/cog-sessions/in`, {
      note: note ?? null
    }, {
      withCredentials: true
    });
  }

  cogOut(note?: string | null): Observable<CogSession> {
    return this.http.post<CogSession>(`${this.baseUrl}/api/economy/cog-sessions/out`, {
      note: note ?? null
    }, {
      withCredentials: true
    });
  }

  getCogSessionHistory(take = 50): Observable<CogSession[]> {
    return this.http.get<CogSession[]>(`${this.baseUrl}/api/economy/cog-sessions/history`, {
      withCredentials: true,
      params: {
        take
      }
    });
  }

  getCogCheckStatus(): Observable<CogCheckStatus> {
    return this.http.get<CogCheckStatus>(`${this.baseUrl}/api/economy/cog-sessions/status`, {
      withCredentials: true
    });
  }

  completeCogCheck(spinsCompleted: number): Observable<CogCheckStatus> {
    return this.http.post<CogCheckStatus>(`${this.baseUrl}/api/economy/cog-sessions/check-in`, {
      spinsCompleted
    }, {
      withCredentials: true
    });
  }

  getAdminUsers(): Observable<AdminUserSummary[]> {
    return this.http.get<AdminUserSummary[]>(`${this.baseUrl}/api/admin/users`, {
      withCredentials: true
    });
  }

  getAdminGearItems(includeInactive: boolean): Observable<StoreItem[]> {
    return this.http.get<StoreItem[]>(`${this.baseUrl}/api/admin/gear-items`, {
      withCredentials: true,
      params: {
        includeInactive
      }
    });
  }

  getCogRuntimeSettings(): Observable<CogRuntimeSettings> {
    return this.http.get<CogRuntimeSettings>(`${this.baseUrl}/api/admin/cog-settings`, {
      withCredentials: true
    });
  }

  updateCogWarningInterval(payload: UpdateCogWarningIntervalPayload): Observable<CogRuntimeSettings> {
    return this.http.put<CogRuntimeSettings>(`${this.baseUrl}/api/admin/cog-settings/warning-interval`, payload, {
      withCredentials: true
    });
  }

  grantCogs(payload: GrantCogsPayload): Observable<AdminUserSummary> {
    return this.http.post<AdminUserSummary>(`${this.baseUrl}/api/admin/grant-cogs`, payload, {
      withCredentials: true
    });
  }

  grantGear(payload: GrantGearPayload): Observable<InventoryItem> {
    return this.http.post<InventoryItem>(`${this.baseUrl}/api/admin/grant-gear`, payload, {
      withCredentials: true
    });
  }

  createGearItem(payload: UpsertGearPayload): Observable<StoreItem> {
    return this.http.post<StoreItem>(`${this.baseUrl}/api/admin/gear-items`, payload, {
      withCredentials: true
    });
  }

  updateGearItem(gearItemId: number, payload: UpsertGearPayload): Observable<StoreItem> {
    return this.http.put<StoreItem>(`${this.baseUrl}/api/admin/gear-items/${gearItemId}`, payload, {
      withCredentials: true
    });
  }
}
