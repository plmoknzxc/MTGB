using System;
using System.Collections.Generic;

[Serializable]
public enum GameChoiceKind
{
    None = 0,
    LibrarySearch = 1,
    DiscardToHandSize = 2,
    SpellTarget = 3,
    CastConfirmation = 4,
    LegendRule = 5,
    TriggerOrder = 6,
    CastFeature = 7,
    SelectOptions = 8,
    ChooseMode = 9,
    ChooseNumber = 10,
    OrderCards = 11,
    DistributeAmount = 12,
    ManaPayment = 13,
    RepeatOptions = 14,
    ReplacementEffect = 15,
    CombatDamageAssignment = 16,
    MulliganDecision = 17,
    MulliganBottom = 18
}

[Serializable]
public enum GameChoiceSelectionMode
{
    Unordered = 0,
    Ordered = 1,
    Number = 2,
    Distribution = 3,
    Repeated = 4
}

[Serializable]
public enum GameChoiceZone
{
    None = 0,
    Library = 1,
    Hand = 2,
    Battlefield = 3,
    Player = 4,
    Graveyard = 5,
    Exile = 6,
    Stack = 7
}

[Serializable]
public sealed class GameChoiceOption
{
    public int optionId;
    public GameChoiceZone zone;
    public int ownerPlayerId;
    public int zoneIndex;
    public bool legal;
    public string label;
    public int minimumAmount;
    public int maximumAmount = 1;
    public long objectId;
    public int objectZoneChangeVersion = -1;
    public long stackObjectId;

    public GameChoiceOption(
        int optionId,
        GameChoiceZone zone,
        int ownerPlayerId,
        int zoneIndex,
        bool legal,
        string label,
        int minimumAmount = 0,
        int maximumAmount = 1,
        long objectId = 0,
        int objectZoneChangeVersion = -1,
        long stackObjectId = 0)
    {
        this.optionId = optionId;
        this.zone = zone;
        this.ownerPlayerId = ownerPlayerId;
        this.zoneIndex = zoneIndex;
        this.legal = legal;
        this.label = label ?? string.Empty;
        this.minimumAmount = Math.Max(0, minimumAmount);
        this.maximumAmount = Math.Max(this.minimumAmount, maximumAmount);
        this.objectId = objectId;
        this.objectZoneChangeVersion = objectZoneChangeVersion;
        this.stackObjectId = stackObjectId;
    }
}

[Serializable]
public sealed class GameChoiceAllocation
{
    public int optionId;
    public int amount;

    public GameChoiceAllocation(int optionId, int amount)
    {
        this.optionId = optionId;
        this.amount = amount;
    }
}

[Serializable]
public sealed class GameChoiceResult
{
    public int requestId;
    public GameChoiceKind kind;
    public int choosingPlayerId;
    public int selectedValue;
    public List<int> selectedOptionIds = new List<int>();
    public List<GameChoiceAllocation> allocations = new List<GameChoiceAllocation>();
}

[Serializable]
public sealed class GameChoiceRequest
{
    public int requestId;
    public int revision;
    public GameChoiceKind kind;
    public GameChoiceSelectionMode selectionMode;
    public int choosingPlayerId;
    public GameChoiceZone sourceZone;
    public int sourcePlayerId;
    public int sourceZoneIndex = -1;
    public string prompt;
    public int minimumSelections;
    public int maximumSelections;
    public bool mayCancel;
    public int minimumValue;
    public int maximumValue;
    public int selectedValue;
    public bool hasSelectedValue;
    public int requiredTotalAmount;
    public List<GameChoiceOption> options = new List<GameChoiceOption>();
    public List<int> selectedOptionIds = new List<int>();
    public List<GameChoiceAllocation> allocations = new List<GameChoiceAllocation>();

    public bool CanConfirm
    {
        get
        {
            switch (selectionMode)
            {
                case GameChoiceSelectionMode.Number:
                    return hasSelectedValue
                        && selectedValue >= minimumValue
                        && selectedValue <= maximumValue;
                case GameChoiceSelectionMode.Distribution:
                    return IsDistributionComplete();
                default:
                    int count = selectedOptionIds.Count;
                    return count >= minimumSelections && count <= maximumSelections;
            }
        }
    }

    public bool IsSelected(int optionId)
    {
        return selectedOptionIds.Contains(optionId);
    }

    public int GetSelectionCount(int optionId)
    {
        int count = 0;
        for (int i = 0; i < selectedOptionIds.Count; i++)
        {
            if (selectedOptionIds[i] == optionId) count++;
        }
        return count;
    }

    public GameChoiceOption FindOption(int optionId)
    {
        for (int i = 0; i < options.Count; i++)
        {
            GameChoiceOption option = options[i];
            if (option != null && option.optionId == optionId) return option;
        }

        return null;
    }

    public bool Toggle(int optionId)
    {
        if (selectionMode == GameChoiceSelectionMode.Number
            || selectionMode == GameChoiceSelectionMode.Distribution)
        {
            return false;
        }

        GameChoiceOption option = FindOption(optionId);
        if (option == null || !option.legal) return false;

        if (selectionMode == GameChoiceSelectionMode.Repeated)
        {
            int count = GetSelectionCount(optionId);
            if (count >= option.maximumAmount
                || selectedOptionIds.Count >= maximumSelections)
            {
                selectedOptionIds.RemoveAll(selected => selected == optionId);
            }
            else
            {
                selectedOptionIds.Add(optionId);
            }
            revision++;
            return true;
        }

        if (selectedOptionIds.Remove(optionId))
        {
            revision++;
            return true;
        }

        if (maximumSelections <= 0) return false;
        if (maximumSelections == 1)
        {
            selectedOptionIds.Clear();
        }
        else if (selectedOptionIds.Count >= maximumSelections)
        {
            return false;
        }

        selectedOptionIds.Add(optionId);
        revision++;
        return true;
    }

    public bool SetNumber(int value)
    {
        if (selectionMode != GameChoiceSelectionMode.Number
            || value < minimumValue
            || value > maximumValue
            || (hasSelectedValue && selectedValue == value))
        {
            return false;
        }

        selectedValue = value;
        hasSelectedValue = true;
        revision++;
        return true;
    }

    public int GetAllocatedAmount(int optionId)
    {
        for (int i = 0; i < allocations.Count; i++)
        {
            GameChoiceAllocation allocation = allocations[i];
            if (allocation != null && allocation.optionId == optionId)
            {
                return allocation.amount;
            }
        }

        return 0;
    }

    public bool SetAllocatedAmount(int optionId, int amount)
    {
        if (selectionMode != GameChoiceSelectionMode.Distribution) return false;

        GameChoiceOption option = FindOption(optionId);
        if (option == null || !option.legal) return false;

        int clampedAmount = Math.Max(0, amount);
        if (clampedAmount > option.maximumAmount) return false;
        if (clampedAmount > 0 && clampedAmount < option.minimumAmount) return false;

        GameChoiceAllocation existing = null;
        for (int i = 0; i < allocations.Count; i++)
        {
            GameChoiceAllocation allocation = allocations[i];
            if (allocation != null && allocation.optionId == optionId)
            {
                existing = allocation;
                break;
            }
        }

        int previousAmount = existing?.amount ?? 0;
        if (previousAmount == clampedAmount) return false;

        int nextTotal = GetAllocatedTotal() - previousAmount + clampedAmount;
        if (requiredTotalAmount >= 0 && nextTotal > requiredTotalAmount) return false;

        if (clampedAmount == 0)
        {
            if (existing != null) allocations.Remove(existing);
        }
        else if (existing == null)
        {
            allocations.Add(new GameChoiceAllocation(optionId, clampedAmount));
        }
        else
        {
            existing.amount = clampedAmount;
        }

        SyncSelectedOptionsFromAllocations();
        revision++;
        return true;
    }

    public int GetAllocatedTotal()
    {
        int total = 0;
        for (int i = 0; i < allocations.Count; i++)
        {
            GameChoiceAllocation allocation = allocations[i];
            if (allocation != null) total += Math.Max(0, allocation.amount);
        }

        return total;
    }

    public bool MoveSelectedOption(int optionId, int destinationIndex)
    {
        if (selectionMode != GameChoiceSelectionMode.Ordered) return false;

        int currentIndex = selectedOptionIds.IndexOf(optionId);
        if (currentIndex < 0
            || destinationIndex < 0
            || destinationIndex >= selectedOptionIds.Count
            || currentIndex == destinationIndex)
        {
            return false;
        }

        selectedOptionIds.RemoveAt(currentIndex);
        selectedOptionIds.Insert(destinationIndex, optionId);
        revision++;
        return true;
    }

    public GameChoiceResult CreateResult()
    {
        if (!CanConfirm) return null;

        var result = new GameChoiceResult
        {
            requestId = requestId,
            kind = kind,
            choosingPlayerId = choosingPlayerId,
            selectedValue = selectedValue
        };
        result.selectedOptionIds.AddRange(selectedOptionIds);
        for (int i = 0; i < allocations.Count; i++)
        {
            GameChoiceAllocation allocation = allocations[i];
            if (allocation != null)
            {
                result.allocations.Add(new GameChoiceAllocation(allocation.optionId, allocation.amount));
            }
        }

        return result;
    }

    private bool IsDistributionComplete()
    {
        int selectedCount = 0;
        for (int i = 0; i < allocations.Count; i++)
        {
            GameChoiceAllocation allocation = allocations[i];
            if (allocation == null || allocation.amount <= 0) continue;

            GameChoiceOption option = FindOption(allocation.optionId);
            if (option == null
                || !option.legal
                || allocation.amount < option.minimumAmount
                || allocation.amount > option.maximumAmount)
            {
                return false;
            }

            selectedCount++;
        }

        return GetAllocatedTotal() == requiredTotalAmount
            && selectedCount >= minimumSelections
            && selectedCount <= maximumSelections;
    }

    private void SyncSelectedOptionsFromAllocations()
    {
        selectedOptionIds.Clear();
        for (int i = 0; i < allocations.Count; i++)
        {
            GameChoiceAllocation allocation = allocations[i];
            if (allocation != null && allocation.amount > 0)
            {
                selectedOptionIds.Add(allocation.optionId);
            }
        }
    }
}
