// ──────────────────────────────────────────────────────────────
// Module: Subscription notifications — security contact and budget
// ──────────────────────────────────────────────────────────────
// Subscription-wide settings whose defaults email the subscription's
// role holders (personal accounts) instead of the ops alias
// (Ownership Boundaries §6 rule 7):
//
//   Defender for Cloud security contact — emails ops; role-based
//     notifications to Owners turned off.
//   Cost Management budget — monthly, notifying the ops action group.
//
// Staging and prod share one subscription, and each of these exists
// once per subscription, so main.bicep deploys this module from one
// environment only (deploySubscriptionNotifications, prod).
// ──────────────────────────────────────────────────────────────
targetScope = 'subscription'

// ── Parameters ────────────────────────────────────────────────

@description('Email addresses for Defender for Cloud security notifications.')
param securityContactEmails array

@description('Resource ID of the action group budget alerts notify.')
param actionGroupId string

@description('Monthly budget in the billing currency (USD) for the whole subscription. 0 = no budget.')
@minValue(0)
param monthlyBudgetAmount int

@description('First day of the month the budget starts, yyyy-MM-01. Keep it fixed: Azure rejects a change to an existing budget\'s start date.')
param budgetStartDate string

// ── Resources ─────────────────────────────────────────────────

resource securityContact 'Microsoft.Security/securityContacts@2020-01-01-preview' = {
  name: 'default'
  properties: {
    emails: join(securityContactEmails, ';')
    alertNotifications: {
      state: 'On'
      minimalSeverity: 'Medium'
    }
    // Off, with no roles: Azure stores an empty list when the state is Off, so
    // naming 'Owner' here would show as drift on every what-if.
    notificationsByRole: {
      state: 'Off'
      roles: []
    }
  }
}

// Thresholds: 80% and 100% of actual spend, and 100% of forecast so a
// runaway month is flagged before it lands.
var budgetThresholds = [
  { key: 'actual80', threshold: 80, type: 'Actual' }
  { key: 'actual100', threshold: 100, type: 'Actual' }
  { key: 'forecast100', threshold: 100, type: 'Forecasted' }
]

resource budget 'Microsoft.Consumption/budgets@2023-11-01' = if (monthlyBudgetAmount > 0) {
  name: 'budget-rvs-monthly'
  properties: {
    category: 'Cost'
    amount: monthlyBudgetAmount
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: budgetStartDate
    }
    notifications: toObject(budgetThresholds, t => t.key, t => {
      enabled: true
      operator: 'GreaterThanOrEqualTo'
      threshold: t.threshold
      thresholdType: t.type
      contactGroups: [
        actionGroupId
      ]
    })
  }
}
