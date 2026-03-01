using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ThemeParkGame.Core;

namespace ThemeParkGame.Visitor
{
    /// <summary>
    /// Represents a group of visitors that travel and make decisions together.
    /// </summary>
    [System.Serializable]
    public class VisitorGroup
    {
        public int GroupId;
        public List<int> MemberIds = new List<int>();
        public Dictionary<int, GroupRole> Roles = new Dictionary<int, GroupRole>();
        public int LeaderId;
        public VisitorType DominantType;
        public RelationshipGroup RelationType;
        public float GroupCohesion;
        public float GroupHappiness;
        public bool IsActive;

        public VisitorGroup(int groupId)
        {
            GroupId = groupId;
            GroupCohesion = 1f;
            GroupHappiness = 50f;
            IsActive = true;
        }
    }

    /// <summary>
    /// Singleton MonoBehaviour that manages visitor group formation, cohesion,
    /// decision synchronization, and happiness bonuses for grouped visitors.
    /// </summary>
    public class GroupBehaviorSystem : MonoBehaviour
    {
        public static GroupBehaviorSystem Instance { get; private set; }

        // Events
        public Action<VisitorGroup> OnGroupFormed;
        public Action<int> OnGroupDisbanded;

        // Configuration
        public const int MaxGroupSize = 6;
        public const float CohesionDistance = 8f;
        public const float GroupFormationChance = 0.3f;

        // Internal state
        private Dictionary<int, VisitorGroup> _groups = new Dictionary<int, VisitorGroup>();
        private Dictionary<int, int> _visitorToGroup = new Dictionary<int, int>();
        private int _nextGroupId = 1;

        // Decision cycle timer for split checks
        private float _decisionCycleTimer = 0f;
        private const float DecisionCycleInterval = 5f;

        #region Singleton Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            WebGLOptimizer.LogVerbose("[GroupBehaviorSystem] Singleton instance initialized.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                WebGLOptimizer.LogVerbose("[GroupBehaviorSystem] Singleton instance destroyed.");
            }
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (_groups.Count == 0)
                return;

            _decisionCycleTimer += Time.deltaTime;
            bool runDecisionCycle = _decisionCycleTimer >= DecisionCycleInterval;
            if (runDecisionCycle)
            {
                _decisionCycleTimer = 0f;
            }

            List<int> groupsToDisband = new List<int>();

            foreach (var kvp in _groups)
            {
                VisitorGroup group = kvp.Value;
                if (!group.IsActive)
                    continue;

                // Update cohesion based on member positions
                UpdateGroupCohesion(group);

                // Update group happiness from member happiness values
                UpdateGroupHappiness(group);

                // Make followers move toward leader if they are too far away
                EnforceFollowerCohesion(group);

                // Sync group decisions when the leader changes state
                SyncGroupDecision(group);

                // Decision cycle checks: split chance if unhappy members
                if (runDecisionCycle)
                {
                    if (CheckGroupSplitCondition(group))
                    {
                        groupsToDisband.Add(group.GroupId);
                    }

                    // SchoolTrip: if leader has left the park, disband entire group
                    if (group.RelationType == RelationshipGroup.SchoolTrip)
                    {
                        VisitorAI leaderAI = GetVisitorAI(group.LeaderId);
                        if (leaderAI == null || leaderAI.CurrentState == VisitorBehaviorState.Leaving)
                        {
                            WebGLOptimizer.LogVerbose(
                                $"[GroupBehaviorSystem] SchoolTrip group {group.GroupId} disbanding because leader left.");
                            ForceAllMembersLeave(group);
                            groupsToDisband.Add(group.GroupId);
                        }
                    }
                }
            }

            // Disband flagged groups outside the iteration
            for (int i = 0; i < groupsToDisband.Count; i++)
            {
                DisbandGroup(groupsToDisband[i]);
            }
        }

        #endregion

        #region Group Formation

        /// <summary>
        /// Creates a new visitor group from the given member IDs and relationship type.
        /// Assigns roles based on the relationship type rules.
        /// </summary>
        public VisitorGroup CreateGroup(List<int> memberIds, RelationshipGroup relationType)
        {
            if (relationType == RelationshipGroup.Solo)
            {
                WebGLOptimizer.LogVerbose("[GroupBehaviorSystem] Cannot create a group for Solo visitors.");
                return null;
            }

            if (memberIds == null || memberIds.Count < 2)
            {
                WebGLOptimizer.LogVerbose("[GroupBehaviorSystem] Cannot create group with fewer than 2 members.");
                return null;
            }

            if (memberIds.Count > MaxGroupSize)
            {
                WebGLOptimizer.LogVerbose(
                    $"[GroupBehaviorSystem] Clamping group size from {memberIds.Count} to {MaxGroupSize}.");
                memberIds = memberIds.GetRange(0, MaxGroupSize);
            }

            // Validate member count against relationship type constraints
            if (!ValidateMemberCount(memberIds.Count, relationType))
            {
                WebGLOptimizer.LogVerbose(
                    $"[GroupBehaviorSystem] Invalid member count {memberIds.Count} for {relationType}.");
                return null;
            }

            // Remove members from any existing groups
            for (int i = 0; i < memberIds.Count; i++)
            {
                if (_visitorToGroup.ContainsKey(memberIds[i]))
                {
                    RemoveMember(memberIds[i]);
                }
            }

            int groupId = _nextGroupId++;
            VisitorGroup group = new VisitorGroup(groupId);
            group.MemberIds = new List<int>(memberIds);
            group.RelationType = relationType;

            // Assign roles based on relationship type
            AssignRoles(group);

            // Determine dominant type from the leader
            VisitorAI leaderAI = GetVisitorAI(group.LeaderId);
            group.DominantType = leaderAI != null ? leaderAI.Type : VisitorType.General;

            // Register group and member mappings
            _groups[groupId] = group;
            for (int i = 0; i < group.MemberIds.Count; i++)
            {
                _visitorToGroup[group.MemberIds[i]] = groupId;
            }

            WebGLOptimizer.LogVerbose(
                $"[GroupBehaviorSystem] Group {groupId} formed: {relationType}, {group.MemberIds.Count} members, leader={group.LeaderId}.");

            OnGroupFormed?.Invoke(group);
            return group;
        }

        /// <summary>
        /// Validates that the member count is within acceptable bounds for the given relationship type.
        /// </summary>
        private bool ValidateMemberCount(int count, RelationshipGroup relationType)
        {
            switch (relationType)
            {
                case RelationshipGroup.Family:
                    return count >= 2 && count <= 5;
                case RelationshipGroup.Couple:
                    return count == 2;
                case RelationshipGroup.FriendGroup:
                    return count >= 2 && count <= 4;
                case RelationshipGroup.SchoolTrip:
                    return count >= 3 && count <= 6;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Assigns GroupRole to each member based on the relationship type formation rules.
        /// </summary>
        private void AssignRoles(VisitorGroup group)
        {
            switch (group.RelationType)
            {
                case RelationshipGroup.Family:
                    AssignFamilyRoles(group);
                    break;

                case RelationshipGroup.Couple:
                    AssignCoupleRoles(group);
                    break;

                case RelationshipGroup.FriendGroup:
                    AssignFriendGroupRoles(group);
                    break;

                case RelationshipGroup.SchoolTrip:
                    AssignSchoolTripRoles(group);
                    break;
            }
        }

        /// <summary>
        /// Family: first adult is Leader, children are Child, rest are Followers.
        /// </summary>
        private void AssignFamilyRoles(VisitorGroup group)
        {
            bool leaderAssigned = false;

            for (int i = 0; i < group.MemberIds.Count; i++)
            {
                int memberId = group.MemberIds[i];
                VisitorAI ai = GetVisitorAI(memberId);

                if (!leaderAssigned && ai != null && ai.Type != VisitorType.Child)
                {
                    group.Roles[memberId] = GroupRole.Leader;
                    group.LeaderId = memberId;
                    leaderAssigned = true;
                }
                else if (ai != null && ai.Type == VisitorType.Child)
                {
                    group.Roles[memberId] = GroupRole.Child;
                }
                else
                {
                    group.Roles[memberId] = GroupRole.Follower;
                }
            }

            // Fallback: if no adult was found, assign first member as leader
            if (!leaderAssigned && group.MemberIds.Count > 0)
            {
                int firstId = group.MemberIds[0];
                group.Roles[firstId] = GroupRole.Leader;
                group.LeaderId = firstId;
            }
        }

        /// <summary>
        /// Couple: first member is Leader, second is Follower.
        /// </summary>
        private void AssignCoupleRoles(VisitorGroup group)
        {
            group.Roles[group.MemberIds[0]] = GroupRole.Leader;
            group.LeaderId = group.MemberIds[0];
            group.Roles[group.MemberIds[1]] = GroupRole.Follower;
        }

        /// <summary>
        /// FriendGroup: random member is Leader, rest are Followers.
        /// </summary>
        private void AssignFriendGroupRoles(VisitorGroup group)
        {
            int leaderIndex = UnityEngine.Random.Range(0, group.MemberIds.Count);
            for (int i = 0; i < group.MemberIds.Count; i++)
            {
                int memberId = group.MemberIds[i];
                if (i == leaderIndex)
                {
                    group.Roles[memberId] = GroupRole.Leader;
                    group.LeaderId = memberId;
                }
                else
                {
                    group.Roles[memberId] = GroupRole.Follower;
                }
            }
        }

        /// <summary>
        /// SchoolTrip: first member is Leader (teacher), rest alternate between Follower and Child.
        /// </summary>
        private void AssignSchoolTripRoles(VisitorGroup group)
        {
            group.Roles[group.MemberIds[0]] = GroupRole.Leader;
            group.LeaderId = group.MemberIds[0];

            for (int i = 1; i < group.MemberIds.Count; i++)
            {
                int memberId = group.MemberIds[i];
                VisitorAI ai = GetVisitorAI(memberId);

                if (ai != null && ai.Type == VisitorType.Child)
                {
                    group.Roles[memberId] = GroupRole.Child;
                }
                else
                {
                    group.Roles[memberId] = GroupRole.Follower;
                }
            }
        }

        #endregion

        #region Group Management

        /// <summary>
        /// Disbands a group, removing all member mappings and marking it inactive.
        /// </summary>
        public void DisbandGroup(int groupId)
        {
            if (!_groups.TryGetValue(groupId, out VisitorGroup group))
            {
                WebGLOptimizer.LogVerbose($"[GroupBehaviorSystem] Cannot disband group {groupId}: not found.");
                return;
            }

            group.IsActive = false;

            for (int i = 0; i < group.MemberIds.Count; i++)
            {
                _visitorToGroup.Remove(group.MemberIds[i]);
            }

            _groups.Remove(groupId);

            WebGLOptimizer.LogVerbose(
                $"[GroupBehaviorSystem] Group {groupId} disbanded ({group.RelationType}).");

            OnGroupDisbanded?.Invoke(groupId);
        }

        /// <summary>
        /// Removes a single member from their group. If the group drops below 2 members, it is disbanded.
        /// If the removed member was the leader, a new leader is assigned.
        /// </summary>
        public void RemoveMember(int visitorId)
        {
            if (!_visitorToGroup.TryGetValue(visitorId, out int groupId))
            {
                WebGLOptimizer.LogVerbose(
                    $"[GroupBehaviorSystem] Visitor {visitorId} is not in any group.");
                return;
            }

            if (!_groups.TryGetValue(groupId, out VisitorGroup group))
            {
                _visitorToGroup.Remove(visitorId);
                return;
            }

            group.MemberIds.Remove(visitorId);
            group.Roles.Remove(visitorId);
            _visitorToGroup.Remove(visitorId);

            WebGLOptimizer.LogVerbose(
                $"[GroupBehaviorSystem] Visitor {visitorId} removed from group {groupId}. Remaining: {group.MemberIds.Count}.");

            // If group is too small, disband
            if (group.MemberIds.Count < 2)
            {
                DisbandGroup(groupId);
                return;
            }

            // If the removed member was the leader, assign a new one
            if (group.LeaderId == visitorId)
            {
                AssignNewLeader(group);
            }
        }

        /// <summary>
        /// Assigns a new leader from remaining members, preferring Followers over Children.
        /// </summary>
        private void AssignNewLeader(VisitorGroup group)
        {
            int newLeaderId = -1;

            // Prefer a follower over a child
            for (int i = 0; i < group.MemberIds.Count; i++)
            {
                int memberId = group.MemberIds[i];
                if (group.Roles.TryGetValue(memberId, out GroupRole role) && role == GroupRole.Follower)
                {
                    newLeaderId = memberId;
                    break;
                }
            }

            // Fallback to any member
            if (newLeaderId == -1 && group.MemberIds.Count > 0)
            {
                newLeaderId = group.MemberIds[0];
            }

            if (newLeaderId != -1)
            {
                group.Roles[newLeaderId] = GroupRole.Leader;
                group.LeaderId = newLeaderId;

                WebGLOptimizer.LogVerbose(
                    $"[GroupBehaviorSystem] New leader for group {group.GroupId}: visitor {newLeaderId}.");
            }
        }

        #endregion

        #region Queries

        /// <summary>
        /// Returns true if the visitor is currently part of an active group.
        /// </summary>
        public bool IsInGroup(int visitorId)
        {
            return _visitorToGroup.ContainsKey(visitorId);
        }

        /// <summary>
        /// Returns the group that the specified visitor belongs to, or null if not grouped.
        /// </summary>
        public VisitorGroup GetGroupForVisitor(int visitorId)
        {
            if (_visitorToGroup.TryGetValue(visitorId, out int groupId))
            {
                if (_groups.TryGetValue(groupId, out VisitorGroup group))
                {
                    return group;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the leader ID of the group the specified visitor belongs to, or -1 if not grouped.
        /// </summary>
        public int GetLeaderId(int visitorId)
        {
            VisitorGroup group = GetGroupForVisitor(visitorId);
            return group != null ? group.LeaderId : -1;
        }

        /// <summary>
        /// Returns all currently active groups.
        /// </summary>
        public List<VisitorGroup> GetActiveGroups()
        {
            List<VisitorGroup> activeGroups = new List<VisitorGroup>();
            foreach (var kvp in _groups)
            {
                if (kvp.Value.IsActive)
                {
                    activeGroups.Add(kvp.Value);
                }
            }

            return activeGroups;
        }

        /// <summary>
        /// Returns the total number of active groups.
        /// </summary>
        public int GetTotalGroupCount()
        {
            int count = 0;
            foreach (var kvp in _groups)
            {
                if (kvp.Value.IsActive)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Returns the total number of visitors that are currently in active groups.
        /// </summary>
        public int GetTotalGroupedVisitors()
        {
            int count = 0;
            foreach (var kvp in _groups)
            {
                if (kvp.Value.IsActive)
                    count += kvp.Value.MemberIds.Count;
            }

            return count;
        }

        /// <summary>
        /// Returns a satisfaction bonus for visitors in groups based on group type.
        /// Solo or ungrouped visitors receive 0. Family children receive +10,
        /// Couple members receive +8, FriendGroup +5, SchoolTrip +7.
        /// An additional +5 bonus is applied if group cohesion exceeds 0.8.
        /// </summary>
        public float GetGroupSatisfactionBonus(int visitorId)
        {
            VisitorGroup group = GetGroupForVisitor(visitorId);
            if (group == null || !group.IsActive)
                return 0f;

            float bonus = 0f;

            switch (group.RelationType)
            {
                case RelationshipGroup.Family:
                    // Children in families get a higher bonus
                    if (group.Roles.TryGetValue(visitorId, out GroupRole familyRole) &&
                        familyRole == GroupRole.Child)
                    {
                        bonus = 10f;
                    }
                    else
                    {
                        bonus = 7f;
                    }
                    break;

                case RelationshipGroup.Couple:
                    bonus = 8f;
                    break;

                case RelationshipGroup.FriendGroup:
                    bonus = 5f;
                    break;

                case RelationshipGroup.SchoolTrip:
                    bonus = 7f;
                    break;
            }

            // High cohesion bonus
            if (group.GroupCohesion > 0.8f)
            {
                bonus += 5f;
            }

            return bonus;
        }

        #endregion

        #region Group Behavior Updates

        /// <summary>
        /// Enforces follower cohesion by moving followers toward the leader when
        /// they exceed CohesionDistance.
        /// </summary>
        private void EnforceFollowerCohesion(VisitorGroup group)
        {
            VisitorAI leaderAI = GetVisitorAI(group.LeaderId);
            if (leaderAI == null)
                return;

            Vector3 leaderPosition = leaderAI.transform.position;

            for (int i = 0; i < group.MemberIds.Count; i++)
            {
                int memberId = group.MemberIds[i];
                if (memberId == group.LeaderId)
                    continue;

                VisitorAI followerAI = GetVisitorAI(memberId);
                if (followerAI == null)
                    continue;

                float distance = Vector3.Distance(followerAI.transform.position, leaderPosition);
                if (distance > CohesionDistance)
                {
                    // Direct follower toward the leader position
                    Vector3 direction = (leaderPosition - followerAI.transform.position).normalized;
                    float moveSpeed = distance * 0.5f * Time.deltaTime;
                    followerAI.transform.position += direction * moveSpeed;
                }
            }
        }

        /// <summary>
        /// Synchronizes group decisions: when the leader enters a new behavior state
        /// (queue, attraction, shop, etc.), all followers adopt the same target.
        /// If the leader enters a queue, all followers join that queue.
        /// </summary>
        public void SyncGroupDecision(VisitorGroup group)
        {
            VisitorAI leaderAI = GetVisitorAI(group.LeaderId);
            if (leaderAI == null)
                return;

            VisitorBehaviorState leaderState = leaderAI.CurrentState;

            for (int i = 0; i < group.MemberIds.Count; i++)
            {
                int memberId = group.MemberIds[i];
                if (memberId == group.LeaderId)
                    continue;

                VisitorAI followerAI = GetVisitorAI(memberId);
                if (followerAI == null)
                    continue;

                // Only sync if follower is in a different state than the leader
                if (followerAI.CurrentState != leaderState)
                {
                    followerAI.CurrentState = leaderState;
                    WebGLOptimizer.LogVerbose(
                        $"[GroupBehaviorSystem] Visitor {memberId} synced to leader state: {leaderState}.");
                }
            }
        }

        /// <summary>
        /// Calculates the average distance of members from the leader and updates
        /// the group's cohesion value (0 = scattered, 1 = tightly packed).
        /// </summary>
        public void UpdateGroupCohesion(VisitorGroup group)
        {
            if (group.MemberIds.Count <= 1)
            {
                group.GroupCohesion = 1f;
                return;
            }

            VisitorAI leaderAI = GetVisitorAI(group.LeaderId);
            if (leaderAI == null)
            {
                group.GroupCohesion = 0f;
                return;
            }

            Vector3 leaderPosition = leaderAI.transform.position;
            float totalDistance = 0f;
            int validMembers = 0;

            for (int i = 0; i < group.MemberIds.Count; i++)
            {
                int memberId = group.MemberIds[i];
                if (memberId == group.LeaderId)
                    continue;

                VisitorAI memberAI = GetVisitorAI(memberId);
                if (memberAI == null)
                    continue;

                totalDistance += Vector3.Distance(memberAI.transform.position, leaderPosition);
                validMembers++;
            }

            if (validMembers == 0)
            {
                group.GroupCohesion = 1f;
                return;
            }

            float averageDistance = totalDistance / validMembers;
            // Map average distance to cohesion: 0 distance = 1.0, CohesionDistance or more = 0.0
            group.GroupCohesion = Mathf.Clamp01(1f - (averageDistance / CohesionDistance));
        }

        /// <summary>
        /// Updates the group happiness as the average of all member happiness values.
        /// Applies a +5 bonus if group cohesion exceeds 0.8.
        /// </summary>
        public void UpdateGroupHappiness(VisitorGroup group)
        {
            float totalHappiness = 0f;
            int validMembers = 0;

            for (int i = 0; i < group.MemberIds.Count; i++)
            {
                VisitorAI memberAI = GetVisitorAI(group.MemberIds[i]);
                if (memberAI == null)
                    continue;

                totalHappiness += memberAI.Happiness;
                validMembers++;
            }

            if (validMembers == 0)
            {
                group.GroupHappiness = 0f;
                return;
            }

            group.GroupHappiness = totalHappiness / validMembers;

            // Cohesion bonus: tight groups are happier
            if (group.GroupCohesion > 0.8f)
            {
                group.GroupHappiness += 5f;
            }

            group.GroupHappiness = Mathf.Clamp(group.GroupHappiness, 0f, 100f);
        }

        #endregion

        #region Split and Leave Logic

        /// <summary>
        /// Checks if any member's happiness is below 30, and if so, rolls a 20% chance
        /// to split the group. Returns true if the group should be disbanded.
        /// </summary>
        private bool CheckGroupSplitCondition(VisitorGroup group)
        {
            bool hasUnhappyMember = false;

            for (int i = 0; i < group.MemberIds.Count; i++)
            {
                VisitorAI memberAI = GetVisitorAI(group.MemberIds[i]);
                if (memberAI != null && memberAI.Happiness < 30f)
                {
                    hasUnhappyMember = true;
                    break;
                }
            }

            if (!hasUnhappyMember)
                return false;

            // 20% chance to split per decision cycle when an unhappy member exists
            float roll = UnityEngine.Random.value;
            if (roll < 0.2f)
            {
                WebGLOptimizer.LogVerbose(
                    $"[GroupBehaviorSystem] Group {group.GroupId} splitting due to low happiness (roll={roll:F2}).");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Forces all members of a group into the Leaving state. Used for SchoolTrip
        /// groups when the leader departs.
        /// </summary>
        private void ForceAllMembersLeave(VisitorGroup group)
        {
            for (int i = 0; i < group.MemberIds.Count; i++)
            {
                VisitorAI memberAI = GetVisitorAI(group.MemberIds[i]);
                if (memberAI != null)
                {
                    memberAI.CurrentState = VisitorBehaviorState.Leaving;
                    WebGLOptimizer.LogVerbose(
                        $"[GroupBehaviorSystem] Visitor {group.MemberIds[i]} forced to leave (SchoolTrip leader departed).");
                }
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Retrieves the VisitorAI component for a given visitor ID via VisitorManager.
        /// Returns null if the visitor cannot be found.
        /// </summary>
        private VisitorAI GetVisitorAI(int visitorId)
        {
            if (VisitorManager.Instance == null)
                return null;

            return VisitorManager.Instance.GetVisitorById(visitorId);
        }

        #endregion
    }
}
