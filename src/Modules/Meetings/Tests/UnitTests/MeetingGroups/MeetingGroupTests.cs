﻿using System;
using System.Collections.Generic;
using CompanyName.MyMeetings.Modules.Meetings.Domain.MeetingGroupProposals;
using CompanyName.MyMeetings.Modules.Meetings.Domain.MeetingGroups;
using CompanyName.MyMeetings.Modules.Meetings.Domain.MeetingGroups.Events;
using CompanyName.MyMeetings.Modules.Meetings.Domain.MeetingGroups.Rules;
using CompanyName.MyMeetings.Modules.Meetings.Domain.Meetings;
using CompanyName.MyMeetings.Modules.Meetings.Domain.Meetings.Events;
using CompanyName.MyMeetings.Modules.Meetings.Domain.Members;
using CompanyName.MyMeetings.Modules.Meetings.Domain.UnitTests.SeedWork;
using NUnit.Framework;


namespace CompanyName.MyMeetings.Modules.Meetings.Domain.UnitTests.MeetingGroups
{
    [TestFixture]
    public class MeetingGroupTests : TestBase
    {
        [Test]
        public void EditGeneralAttributes_IsSuccessful()
        {
            var meetingGroup = CreateMeetingGroup();

            var meetingGroupLocation = new MeetingGroupLocation("London", "GB");
            meetingGroup.EditGeneralAttributes("newName", "newDescription", meetingGroupLocation);

            var meetingGroupGeneralAttributesEdited =
                GetPublishedDomainEvent<MeetingGroupGeneralAttributesEditedDomainEvent>(meetingGroup);

            Assert.That(meetingGroupGeneralAttributesEdited.NewName, Is.EqualTo("newName"));
            Assert.That(meetingGroupGeneralAttributesEdited.NewDescription, Is.EqualTo("newDescription"));
            Assert.That(meetingGroupGeneralAttributesEdited.NewLocation, Is.EqualTo(meetingGroupLocation));
        }

        [Test]
        public void JoinToGroup_WhenMemberHasNotJoinedYet_IsSuccessful()
        {
            var meetingGroup = CreateMeetingGroup();

            MemberId newMemberId = new MemberId(Guid.NewGuid());
            meetingGroup.JoinToGroupMember(newMemberId);

            var newMeetingGroupMemberJoined = GetPublishedDomainEvent<NewMeetingGroupMemberJoinedDomainEvent>(meetingGroup);

            Assert.That(newMeetingGroupMemberJoined.MeetingGroupId, Is.EqualTo(meetingGroup.Id));
            Assert.That(newMeetingGroupMemberJoined.MemberId, Is.EqualTo(newMemberId));
            Assert.That(newMeetingGroupMemberJoined.Role, Is.EqualTo(MeetingGroupMemberRole.Member));
        }

        [Test]
        public void JoinToGroup_WhenMemberHasAlreadyJoined_BreaksMeetingGroupMemberCannotBeAddedTwiceRule()
        {
            var meetingGroup = CreateMeetingGroup();

            MemberId newMemberId = new MemberId(Guid.NewGuid());
            meetingGroup.JoinToGroupMember(newMemberId);

            AssertBrokenRule<MeetingGroupMemberCannotBeAddedTwiceRule>(() =>
            {
                meetingGroup.JoinToGroupMember(newMemberId);
            });
        }

        [Test]
        public void LeaveGroup_WhenMemberIsActiveMemberOfGroup_IsSuccessful()
        {
            var meetingGroup = CreateMeetingGroup();
            var newMemberId = new MemberId(Guid.NewGuid());
            meetingGroup.JoinToGroupMember(newMemberId);

            meetingGroup.LeaveGroup(newMemberId);

            var meetingGroupMemberLeft = GetPublishedDomainEvent<MeetingGroupMemberLeftGroupDomainEvent>(meetingGroup);

            Assert.That(meetingGroupMemberLeft.MemberId, Is.EqualTo(newMemberId));
        }

        [Test]
        public void LeaveGroup_WhenMemberIsNotActiveMemberOfGroup_BreaksNotActualGroupMemberCannotLeaveGroupRule()
        {
            var meetingGroup = CreateMeetingGroup();
            var newMemberId = new MemberId(Guid.NewGuid());

            AssertBrokenRule<NotActualGroupMemberCannotLeaveGroupRule>(() =>
            {
                meetingGroup.LeaveGroup(newMemberId);
            });
        }

        [Test]
        public void UpdatePaymentDateTo_IsSuccessful()
        {
            var meetingGroup = CreateMeetingGroup();
            DateTime dateTo = DateTime.UtcNow;

            meetingGroup.UpdatePaymentInfo(dateTo);

            var meetingGroupPaymentInfoUpdated = GetPublishedDomainEvent<MeetingGroupPaymentInfoUpdatedDomainEvent>(meetingGroup);

            Assert.That(meetingGroupPaymentInfoUpdated.MeetingGroupId, Is.EqualTo(meetingGroup.Id));
            Assert.That(meetingGroupPaymentInfoUpdated.PaymentDateTo, Is.EqualTo(dateTo));
        }

        [Test]
        public void CreateMeeting_WhenGroupIsNotPayed_IsNotPossible()
        {
            var meetingGroup = CreateMeetingGroup();

            MemberId creatorId = new MemberId(Guid.NewGuid());

            AssertBrokenRule<MeetingCanBeOrganizedOnlyByPayedGroupRule>(() =>
            {
                meetingGroup.CreateMeeting("title",
                    new MeetingTerm(
                        new DateTime(2019, 1, 1, 10, 0, 0),
                        new DateTime(2019, 1, 1, 12, 0, 0)),
                    "description",
                    new MeetingLocation("Name", "Address", "PostalCode", "City"),
                    null,
                    0,
                    Term.NoTerm,
                    MoneyValue.Zero,
                    new List<MemberId>(),
                    creatorId);
            });
        }

        [Test]
        public void CreateMeeting_WhenCreatorIsMemberOfGroupAndHostsAreNotDefined_IsSuccessful()
        {
            var definedProposalMemberId = new MemberId(Guid.NewGuid());
            var meetingGroup = CreateMeetingGroup(definedProposalMemberId);
            meetingGroup.UpdatePaymentInfo(DateTime.UtcNow.AddDays(1));

            var meeting = meetingGroup.CreateMeeting("title",
                new MeetingTerm(
                    new DateTime(2019, 1, 1, 10, 0, 0),
                    new DateTime(2019, 1, 1, 12, 0, 0)),
                "description",
                new MeetingLocation("Name", "Address", "PostalCode", "City"),
                null,
                0,
                Term.NoTerm,
                MoneyValue.Zero,
                new List<MemberId>(),
                definedProposalMemberId);

            GetPublishedDomainEvent<MeetingCreatedDomainEvent>(meeting);
        }

        [Test]
        public void CreateMeeting_WhenHostsAreDefinedAndTheyAreGroupMembers_DefinedHostsAreHostsOfMeeting()
        {
            var definedProposalMemberId = new MemberId(Guid.NewGuid());
            var meetingGroup = CreateMeetingGroup(definedProposalMemberId);
            meetingGroup.UpdatePaymentInfo(DateTime.UtcNow.AddDays(1));
            var hostOne = new MemberId(Guid.NewGuid());
            var hostTwo = new MemberId(Guid.NewGuid());
            List<MemberId> hosts = new List<MemberId>();
            hosts.Add(hostOne);
            hosts.Add(hostTwo);
            meetingGroup.JoinToGroupMember(hostOne);
            meetingGroup.JoinToGroupMember(hostTwo);

            var meeting = meetingGroup.CreateMeeting("title",
                new MeetingTerm(
                    new DateTime(2019, 1, 1, 10, 0, 0),
                    new DateTime(2019, 1, 1, 12, 0, 0)),
                "description",
                new MeetingLocation("Name", "Address", "PostalCode", "City"),
                null,
                0,
                Term.NoTerm,
                MoneyValue.Zero,
                hosts,
                definedProposalMemberId);

            var meetingAttendeeAddedEvents = GetPublishedDomainEvents<MeetingAttendeeAddedDomainEvent>(meeting);
            Assert.That(meetingAttendeeAddedEvents.Count, Is.EqualTo(2));
            Assert.That(meetingAttendeeAddedEvents[0].AttendeeId, Is.EqualTo(hostOne));
            Assert.That(meetingAttendeeAddedEvents[0].Role, Is.EqualTo(MeetingAttendeeRole.Host));
            Assert.That(meetingAttendeeAddedEvents[1].AttendeeId, Is.EqualTo(hostTwo));
            Assert.That(meetingAttendeeAddedEvents[1].Role, Is.EqualTo(MeetingAttendeeRole.Host));
        }

        [Test]
        public void CreateMeeting_WhenHostsAreDefinedAndTheyAreNotGroupMembers_BreaksMeetingHostMustBeAMeetingGroupMemberRule()
        {
            var definedProposalMemberId = new MemberId(Guid.NewGuid());
            var meetingGroup = CreateMeetingGroup(definedProposalMemberId);
            meetingGroup.UpdatePaymentInfo(DateTime.UtcNow.AddDays(1));
            var hostOne = new MemberId(Guid.NewGuid());
            var hostTwo = new MemberId(Guid.NewGuid());
            List<MemberId> hosts = new List<MemberId>();
            hosts.Add(hostOne);
            hosts.Add(hostTwo);

            AssertBrokenRule<MeetingHostMustBeAMeetingGroupMemberRule>(() =>
            {
                meetingGroup.CreateMeeting("title",
                    new MeetingTerm(
                        new DateTime(2019, 1, 1, 10, 0, 0),
                        new DateTime(2019, 1, 1, 12, 0, 0)),
                    "description",
                    new MeetingLocation("Name", "Address", "PostalCode", "City"),
                    null,
                    0,
                    Term.NoTerm,
                    MoneyValue.Zero,
                    hosts,
                    definedProposalMemberId);
            });
        }

        [Test]
        public void CreateMeeting_WhenCreatorIsNotMemberOfGroup_BreaksMeetingHostMustBeAMeetingGroupMemberRule()
        {
            var definedProposalMemberId = new MemberId(Guid.NewGuid());
            var creatorId = new MemberId(Guid.NewGuid());
            var meetingGroup = CreateMeetingGroup(definedProposalMemberId);
            meetingGroup.UpdatePaymentInfo(DateTime.UtcNow.AddDays(1));

            AssertBrokenRule<MeetingHostMustBeAMeetingGroupMemberRule>(() =>
            {
                meetingGroup.CreateMeeting("title",
                    new MeetingTerm(
                        new DateTime(2019, 1, 1, 10, 0, 0),
                        new DateTime(2019, 1, 1, 12, 0, 0)),
                    "description",
                    new MeetingLocation("Name", "Address", "PostalCode", "City"),
                    null,
                    0,
                    Term.NoTerm,
                    MoneyValue.Zero,
                    new List<MemberId>(),
                    creatorId);
            });
        }

        private static MeetingGroup CreateMeetingGroup(MemberId definedProposalMemberId = null)
        {
            var proposalMemberId = definedProposalMemberId ?? new MemberId(Guid.NewGuid());
            var meetingProposal = MeetingGroupProposal.ProposeNew(
                "name", "description",
                new MeetingGroupLocation("Warsaw", "PL"), proposalMemberId);

            meetingProposal.Accept();

            var meetingGroup = meetingProposal.CreateMeetingGroup();

            DomainEventsTestHelper.ClearAllDomainEvents(meetingGroup);

            return meetingGroup;
        }
    }
}