import { CommonModule } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { catchError, firstValueFrom, forkJoin, of } from 'rxjs';
import {
  AdminUserSummary,
  CogSession,
  CraftGearPayload,
  CreateMarketplaceListingPayload,
  DashboardResponse,
  GrantCogsPayload,
  GrantGearPayload,
  MarketplaceListing,
  StoreItem,
  UpsertGearPayload,
  UserProfile
} from './models/economy.models';
import { EconomyApiService } from './services/economy-api.service';

type CogPage = 'pilot' | 'shop' | 'locker' | 'marketplace' | 'timecard' | 'admin';

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
  activePage: CogPage = 'pilot';

  dashboard: DashboardResponse | null = null;
  marketplaceListings: MarketplaceListing[] = [];
  cogSessionHistory: CogSession[] = [];
  adminUsers: AdminUserSummary[] = [];
  adminGearItems: StoreItem[] = [];

  grantCogsForm: GrantCogsPayload = {
    userAccountId: 0,
    amount: 25,
    note: 'Standard disbursement per Central Authority directive.'
  };

  grantGearForm: GrantGearPayload = {
    userAccountId: 0,
    gearItemId: 0,
    quantity: 1,
    note: 'Authorized gear allocation - see case file.'
  };

  gearForm: UpsertGearPayload = this.createEmptyGearForm();
  editingGearItemId: number | null = null;

  craftForm: CraftGearPayload = this.createEmptyCraftForm();

  listingForm: CreateMarketplaceListingPayload = {
    gearItemId: 0,
    quantity: 1,
    priceInCogs: 40,
    sellerNote: 'Fresh from my cog forge.'
  };

  cogInNote = '';
  cogOutNote = '';

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

  get openCogSession(): CogSession | null {
    return this.cogSessionHistory.find(x => x.isOpen) ?? null;
  }

  get hasOpenCogSession(): boolean {
    return this.openCogSession !== null;
  }

  get selectedListingItem(): UserProfile['inventory'][number] | null {
    const inventory = this.pilot?.inventory ?? [];
    return inventory.find(x => x.gearItemId === this.listingForm.gearItemId) ?? null;
  }

  get liveCogBalance(): number {
    return this.pilot?.cogBalance ?? 0;
  }

  get adminHeadcount(): number {
    return this.adminUsers.length;
  }

  get cogCirculation(): number {
    return this.adminUsers.reduce((total, user) => total + user.cogBalance, 0);
  }

  get selectedGrantCogsTarget(): AdminUserSummary | null {
    return this.adminUsers.find(x => x.userAccountId === this.grantCogsForm.userAccountId) ?? null;
  }

  get selectedGrantGearTarget(): AdminUserSummary | null {
    return this.adminUsers.find(x => x.userAccountId === this.grantGearForm.userAccountId) ?? null;
  }

  setPage(page: CogPage): void {
    if (page === 'admin' && !this.hasAdminPanel) {
      return;
    }

    this.activePage = page;
  }

  isPage(page: CogPage): boolean {
    return this.activePage === page;
  }

  signIn(): void {
    this.infoMessage = 'Redirecting to identity verification...';
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
    this.marketplaceListings = [];
    this.cogSessionHistory = [];
    this.adminUsers = [];
    this.adminGearItems = [];
    this.activePage = 'pilot';
    this.infoMessage = 'Session terminated. Your cogs remain on file with the Authority.';
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
        this.marketplaceListings = [];
        this.cogSessionHistory = [];
        this.adminUsers = [];
        this.adminGearItems = [];
        this.activePage = 'pilot';
        this.infoMessage = 'No active session. The economy continues without you.';
        return;
      }

      this.isAuthenticated = true;
      this.dashboard = dashboard;
      await this.loadMarketplaceAndSessions();
      this.ensureListingFormTargets();

      if (dashboard.pilot.isAdmin) {
        await this.loadAdminPanel();
      } else {
        this.adminUsers = [];
        this.adminGearItems = [];

        if (this.activePage === 'admin') {
          this.activePage = 'pilot';
        }
      }
    } catch (error) {
      this.captureError(error, 'The cog reserve encountered a structural integrity failure.');
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
      this.captureError(error, 'Transaction rejected by the cog clearinghouse.');
    }
  }

  async craftGear(): Promise<void> {
    this.errorMessage = '';

    const payload: CraftGearPayload = {
      ...this.craftForm,
      name: this.craftForm.name.trim(),
      gearType: this.craftForm.gearType.trim(),
      description: this.normalizeText(this.craftForm.description),
      flavorText: this.normalizeText(this.craftForm.flavorText)
    };

    if (!payload.name || !payload.gearType || payload.craftingCostInCogs < 1) {
      this.errorMessage = 'Crafting requires a name, gear type, and positive cog cost.';
      return;
    }

    try {
      const receipt = await firstValueFrom(this.api.craftGear(payload));
      this.infoMessage = receipt.message;
      this.craftForm = this.createEmptyCraftForm();
      await this.refreshAll();
      this.setPage('locker');
    } catch (error) {
      this.captureError(error, 'Crafting failed. The forge rejected your schematic.');
    }
  }

  async createMarketplaceListing(): Promise<void> {
    this.errorMessage = '';
    const selectedItem = this.selectedListingItem;

    if (!selectedItem) {
      this.errorMessage = 'Choose a gear item from your locker before listing.';
      return;
    }

    const quantity = Math.floor(this.listingForm.quantity);
    const priceInCogs = Math.floor(this.listingForm.priceInCogs);

    if (quantity < 1 || quantity > selectedItem.quantity) {
      this.errorMessage = 'Listing quantity must be at least 1 and within your locker holdings.';
      return;
    }

    if (priceInCogs < 1) {
      this.errorMessage = 'Listing price must be a positive cog amount.';
      return;
    }

    try {
      await firstValueFrom(this.api.createMarketplaceListing({
        gearItemId: selectedItem.gearItemId,
        quantity,
        priceInCogs,
        sellerNote: this.normalizeText(this.listingForm.sellerNote)
      }));

      this.infoMessage = 'Listing posted. The cog bazaar is now watching.';
      await this.refreshAll();
      this.setPage('marketplace');
    } catch (error) {
      this.captureError(error, 'Listing creation failed. Marketplace registry denied the submission.');
    }
  }

  async buyMarketplaceListing(listing: MarketplaceListing): Promise<void> {
    this.errorMessage = '';

    try {
      const receipt = await firstValueFrom(this.api.buyMarketplaceListing(listing.marketplaceListingId));
      this.infoMessage = receipt.message;
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'Marketplace purchase denied by the exchange engine.');
    }
  }

  async cogIn(): Promise<void> {
    this.errorMessage = '';

    try {
      await firstValueFrom(this.api.cogIn(this.normalizeText(this.cogInNote)));
      this.infoMessage = 'You are now cogged in. Shift timer engaged.';
      this.cogInNote = '';
      await this.refreshAll();
      this.setPage('timecard');
    } catch (error) {
      this.captureError(error, 'Cog-in failed. Shift authorization denied.');
    }
  }

  async cogOut(): Promise<void> {
    this.errorMessage = '';

    try {
      const session = await firstValueFrom(this.api.cogOut(this.normalizeText(this.cogOutNote)));
      this.infoMessage = `Cog-out recorded. Shift duration: ${this.formatDurationMinutes(session.durationMinutes)}.`;
      this.cogOutNote = '';
      await this.refreshAll();
      this.setPage('timecard');
    } catch (error) {
      this.captureError(error, 'Cog-out failed. No active shift was found.');
    }
  }

  listingOwnershipLabel(listing: MarketplaceListing): string {
    if (listing.sellerUserAccountId === this.pilot?.userAccountId) {
      return 'Your listing';
    }

    return `Seller: ${listing.sellerDisplayName}`;
  }

  isOwnListing(listing: MarketplaceListing): boolean {
    return listing.sellerUserAccountId === this.pilot?.userAccountId;
  }

  canAffordListing(listing: MarketplaceListing): boolean {
    return (this.pilot?.cogBalance ?? 0) >= listing.priceInCogs;
  }

  listingQuantityOptions(itemQuantity: number): number[] {
    const upperBound = Math.max(1, Math.min(itemQuantity, 100));
    return Array.from({ length: upperBound }, (_, idx) => idx + 1);
  }

  onListingGearChanged(): void {
    this.ensureListingFormTargets();
  }

  sessionDurationLabel(session: CogSession): string {
    if (session.isOpen) {
      const startedAt = this.toUtcMilliseconds(session.cogInAtUtc);
      if (startedAt === null) {
        return 'In progress';
      }

      const elapsedMinutes = Math.max(0, Math.floor((Date.now() - startedAt) / 60000));
      return `${this.formatDurationMinutes(elapsedMinutes)} elapsed`;
    }

    return this.formatDurationMinutes(session.durationMinutes);
  }

  async grantCogs(): Promise<void> {
    if (!this.grantCogsForm.userAccountId || this.grantCogsForm.amount < 1) {
      this.errorMessage = 'Disbursement requires a designated recipient and a positive cog amount.';
      return;
    }

    this.errorMessage = '';

    try {
      await firstValueFrom(this.api.grantCogs(this.grantCogsForm));
      this.infoMessage = 'Cogs disbursed. The recipient has been notified by the usual channels.';
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'Disbursement denied. The cog reserve has flagged this transaction.');
    }
  }

  async grantGear(): Promise<void> {
    if (!this.grantGearForm.userAccountId || !this.grantGearForm.gearItemId) {
      this.errorMessage = 'Allocation requires both a recipient and a gear specification.';
      return;
    }

    this.errorMessage = '';

    try {
      await firstValueFrom(this.api.grantGear(this.grantGearForm));
      this.infoMessage = 'Gear allocated and transferred. Inventory records updated.';
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'Gear allocation rejected by supply chain authority.');
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
    this.infoMessage = 'Gear loaded for editing.';
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
      this.errorMessage = 'Gear name and type are required fields.';
      return;
    }

    try {
      if (this.editingGearItemId === null) {
        await firstValueFrom(this.api.createGearItem(payload));
        this.infoMessage = 'New gear commissioned and entered into the official registry.';
      } else {
        await firstValueFrom(this.api.updateGearItem(this.editingGearItemId, payload));
        this.infoMessage = 'Gear updated. The economy adjusts.';
      }

      this.cancelGearEdit();
      await this.refreshAll();
    } catch (error) {
      this.captureError(error, 'The registry has refused this specification. Review and resubmit.');
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

  private async loadMarketplaceAndSessions(): Promise<void> {
    const data = await firstValueFrom(
      forkJoin({
        listings: this.api.getMarketplaceListings(),
        sessions: this.api.getCogSessionHistory(100)
      })
    );

    this.marketplaceListings = data.listings;
    this.cogSessionHistory = data.sessions;
  }

  private ensureListingFormTargets(): void {
    const inventory = this.pilot?.inventory ?? [];

    if (inventory.length === 0) {
      this.listingForm.gearItemId = 0;
      this.listingForm.quantity = 1;
      return;
    }

    const hasSelected = inventory.some(x => x.gearItemId === this.listingForm.gearItemId);
    if (!hasSelected) {
      this.listingForm.gearItemId = inventory[0].gearItemId;
    }

    const selected = this.selectedListingItem;
    if (selected && this.listingForm.quantity > selected.quantity) {
      this.listingForm.quantity = selected.quantity;
    }

    if (!selected || this.listingForm.quantity < 1) {
      this.listingForm.quantity = 1;
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

  private formatDurationMinutes(durationMinutes: number | null | undefined): string {
    if (durationMinutes === null || durationMinutes === undefined) {
      return 'In progress';
    }

    const normalizedMinutes = Math.max(0, Math.floor(durationMinutes));
    const hours = Math.floor(normalizedMinutes / 60);
    const minutes = normalizedMinutes % 60;

    if (hours === 0) {
      return `${minutes}m`;
    }

    if (minutes === 0) {
      return `${hours}h`;
    }

    return `${hours}h ${minutes}m`;
  }

  private toUtcMilliseconds(value: string | null | undefined): number | null {
    if (!value) {
      return null;
    }

    const hasOffset = /([zZ]|[+-]\d{2}:\d{2})$/.test(value);
    const normalized = hasOffset ? value : `${value}Z`;
    const parsed = Date.parse(normalized);
    return Number.isNaN(parsed) ? null : parsed;
  }

  private createEmptyGearForm(): UpsertGearPayload {
    return {
      name: '',
      description: '',
      gearType: 'Trinket',
      costInCogs: 10,
      stockQuantity: null,
      isActive: true,
      flavorText: 'Manufactured under license from the Central Cog Authority.'
    };
  }

  private createEmptyCraftForm(): CraftGearPayload {
    return {
      name: '',
      description: '',
      gearType: 'Custom',
      craftingCostInCogs: 35,
      flavorText: 'One-off forge run from my personal cog press.'
    };
  }
}
