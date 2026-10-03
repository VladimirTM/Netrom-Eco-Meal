export interface PackageDto {
  id: string;
  businessId: string;
  businessName: string;
  packageTypeId: string;
  packageTypeName: string;
  name: string;
  description: string;
  price: number;
  quantity: number;
  weightKg: number;
  dietaryTags: string[];
  // DateTime fields serialize as UTC ISO 8601 ("Z").
  pickupStart: string;
  pickupEnd: string;
  imageUrl: string | null;
  templateId: string | null;
  isHidden: boolean;
  hiddenReason: string | null;
  markdownDismissedAt: string | null;
  donatedAt: string | null;
  donationOfferedAt: string | null;
}

export interface ReviewDto {
  id: string;
  businessId: string;
  userId: string;
  userName: string;
  rating: number;
  comment: string | null;
  createdAt: string;
  packageId: string | null;
}

export interface ReviewContextDto {
  canReview: boolean;
  myReview: ReviewDto | null;
  reviewablePackages: PackageDto[];
}

export interface KitchenTipDto {
  id: string;
  businessId: string;
  userId: string;
  userName: string;
  tip: string;
  createdAt: string;
}
