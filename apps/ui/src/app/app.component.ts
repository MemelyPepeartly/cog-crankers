import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { catchError, firstValueFrom, forkJoin, of } from 'rxjs';
import {
  AdminUserSummary,
  DashboardResponse,
  GrantCogsPayload,
  GrantGearPayload,
  StoreItem,
  UpsertGearPayload,
  UserProfile
} from './models/economy.models';
import { EconomyApiService } from './services/economy-api.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit {
  private readonly api = inject(EconomyApiService);

  isLoading = true;
  isAuthenticated = false;
  errorMessage = '';
  infoMessage = '';

  dashboard: DashboardResponse | null = null;
  adminUsers: AdminUserSummary[] = [];
  adminGearItems: StoreItem[] = [];

  grantCogsForm: GrantCogsPayload = {
    userAccountId: 0,
    amount: 25,
    note: 'Fresh cogs from mission control.'
  };

  grantGearForm: GrantGearPayload = {
    userAccountId: 0,
    gearItemId: 0,
    quantity: 1,
    note: 'Special workshop drop.'
  };

  gearForm: UpsertGearPayload = this.createEmptyGearForm();
  editingGearItemId: number | null = null;

  async ngOnInit(): Promise<void> {
    await this.refreshAll();
  }

  get pilot(): UserProfile | null {
    return this.dashboard?.pilot ?? null;
  }

  get storeFront(): StoreItem[] {
    return this.dashboard?.storeFront ?? [];
  }

  get hasAdminPanel(): boolean {
    return this.pilot?.isAdmin ?? false;
  }

  signIn(): void {
    this.api.startGoogleLogin();
  }

  async signOut(): Promise<void> {
    this.errorMessage = '';

    try {
      await firstValueFrom(this.api.logout());
    } catch {
      // Sign-out still proceeds client-side so the user can re-enter auth flow.
    }

    this.isAuthenticated = false;
    this.dashboard = null;
    this.adminUsers = [];
    this.adminGearItems = [];
    this.infoMessage = 'Clutch released. Come back when you are ready to crank again.';
  }

  async refreshAll(): Promise<void> {
    this.isLoading = true;
    this.errorMessage = '';

    try {
      const dashboard = await firstValueFrom(
        this.api.getDashboard().pipe(
          catchError((error: HttpErrorResponse) => {
            if (error.status === 401) {
              return of(null);
            }

            throw error;
          })
        )
      );

      if (!dashboard) {
        this.isAuthenticated = false;
        this.dashboard = null;
        this.adminUsers = [];
        this.adminGearItems = [];
        return;
      }

      this.isAuthenticated = true;
      this.dashboard = dashboard;

      if (dashboard.pilot.isAdmin) {
        await this.loadAdminPanel();
      }
    } catch (error) {
      this.captureError(error, 'The control deck jammed while loading.');
    } finally {
      this.isLoading = false;
    }
  }

  async buyGear(item: StoreItem): Promise<void> {
    this.errorMessage = '';

    try {
      const receipt = await firstValueFrom(this.api.buyGear(item.gearItemId, 1));
      this.infoMessage = receipt.message;
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'Purchase gears slipped out of alignment.');
    }
  }

  async grantCogs(): Promise<void> {
    if (!this.grantCogsForm.userAccountId || this.grantCogsForm.amount < 1) {
      this.errorMessage = 'Select a user and enter a positive cog amount.';
      return;
    }

    this.errorMessage = '';

    try {
      await firstValueFrom(this.api.grantCogs(this.grantCogsForm));
      this.infoMessage = 'Cog stipend delivered to the selected pilot.';
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'Cog grant failed to engage.');
    }
  }

  async grantGear(): Promise<void> {
    if (!this.grantGearForm.userAccountId || !this.grantGearForm.gearItemId) {
      this.errorMessage = 'Select a user and a gear item before granting.';
      return;
    }

    this.errorMessage = '';

    try {
      await firstValueFrom(this.api.grantGear(this.grantGearForm));
      this.infoMessage = 'Gear parcel launched.';
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'Gear grant failed to deploy.');
    }
  }

  startEditGearItem(item: StoreItem): void {
    this.editingGearItemId = item.gearItemId;
    this.gearForm = {
      name: item.name,
      description: item.description ?? '',
      gearType: item.gearType,
      costInCogs: item.costInCogs,
      stockQuantity: item.stockQuantity ?? null,
      isActive: item.isActive,
      flavorText: item.flavorText ?? ''
    };
    this.infoMessage = 'Editing gear profile. Save to forge updates.';
  }

  cancelGearEdit(): void {
    this.editingGearItemId = null;
    this.gearForm = this.createEmptyGearForm();
  }

  async saveGearItem(): Promise<void> {
    this.errorMessage = '';

    const payload = {
      ...this.gearForm,
      name: this.gearForm.name.trim(),
      gearType: this.gearForm.gearType.trim(),
      description: this.normalizeText(this.gearForm.description),
      flavorText: this.normalizeText(this.gearForm.flavorText),
      stockQuantity: this.normalizeStock(this.gearForm.stockQuantity)
    };

    if (!payload.name || !payload.gearType) {
      this.errorMessage = 'Gear name and gear type are required.';
      return;
    }

    try {
      if (this.editingGearItemId === null) {
        await firstValueFrom(this.api.createGearItem(payload));
        this.infoMessage = 'New gear forged in the economy.';
      } else {
        await firstValueFrom(this.api.updateGearItem(this.editingGearItemId, payload));
        this.infoMessage = 'Gear entry tightened and re-calibrated.';
      }

      this.cancelGearEdit();
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'Could not save the gear profile.');
    }
  }

  private async loadAdminPanel(): Promise<void> {
    const data = await firstValueFrom(
      forkJoin({
        users: this.api.getAdminUsers(),
        gearItems: this.api.getAdminGearItems(true)
      })
    );

    this.adminUsers = data.users;
    this.adminGearItems = data.gearItems;

    if (this.adminUsers.length > 0) {
      if (!this.grantCogsForm.userAccountId) {
        this.grantCogsForm.userAccountId = this.adminUsers[0].userAccountId;
      }

      if (!this.grantGearForm.userAccountId) {
        this.grantGearForm.userAccountId = this.adminUsers[0].userAccountId;
      }
    }

    if (this.adminGearItems.length > 0 && !this.grantGearForm.gearItemId) {
      this.grantGearForm.gearItemId = this.adminGearItems[0].gearItemId;
    }
  }

  private normalizeText(value: string | null | undefined): string | null {
    const trimmed = value?.trim();
    return trimmed ? trimmed : null;
  }

  private normalizeStock(value: number | null | undefined): number | null {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return null;
    }

    return Math.max(0, Math.floor(value));
  }

  private captureError(error: unknown, fallbackMessage: string): void {
    this.infoMessage = '';

    if (error instanceof HttpErrorResponse) {
      const apiMessage = (error.error as { message?: string } | null)?.message;
      this.errorMessage = apiMessage ?? fallbackMessage;
      return;
    }

    this.errorMessage = fallbackMessage;
  }

  private createEmptyGearForm(): UpsertGearPayload {
    return {
      name: '',
      description: '',
      gearType: 'Trinket',
      costInCogs: 10,
      stockQuantity: null,
      isActive: true,
      flavorText: 'Fresh from the cog forge.'
    };
  }
}
