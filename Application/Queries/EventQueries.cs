#nullable disable
#pragma warning disable IDE0028

using Dapper;
using ETMP.Models.Events;
using Events.API.Helpers;

namespace Events.API.Application.Queries
{
    public interface IEventQueries
    {
        Task<EventResponseModel> GetEventDapperAsync(long eventId);
        Task<SessionResponseModel> GetSessionDapperAsync(long eventId, long sessionId);
        Task<IList<EventResponseModel>> GetEventsDapperAsync();
    }
    public class EventQueries(IConfiguration configuration) : IEventQueries
    {
        public async Task<EventResponseModel> GetEventDapperAsync(long eventId)
        {
            var eventModel = new EventResponseModel();

            using (var con = new DataContext(configuration).CreateConnection())
            {
                try
                {
                    long currentEventId = -1;
                    var eventModelDto = await con.QueryAsync<EventModelDto>(
                        "SELECT* FROM public.fngetevent(@p_eventid)",
                        new
                        {
                            p_eventid = eventId
                        });

                    foreach (var eventDto in eventModelDto)
                    {
                        if (currentEventId != eventDto.EventId)
                        {
                            eventModel = new EventResponseModel()
                            {
                                Description = eventDto.EventDescription,
                                Name = eventDto.EventName,
                                Id = eventDto.EventId,
                                Slug = eventDto.Slug,
                                PubliciseStart = eventDto.PubliciseStart,
                                PubliciseEnd = eventDto.PubliciseEnd,
                                Images = new List<ImageResponseModel>(),
                                Sessions = new List<SessionResponseModel>(),
                                PasswordEnabled = eventDto.PasswordEnabled,
                                MD5String = eventDto.MD5String
                            };
                            eventModel.Sessions.Add(new SessionResponseModel()
                            {
                                Id = eventDto.SessionId,
                                Start = eventDto.StartTime,
                                Description = eventDto.SessionDescription,
                                AdditionalInformation = eventDto.AdditionalInformation,
                                IsSoldout = eventDto.IsSoldOut,
                                MaxTickets = eventDto.MaxTickets,
                                IsPaymentPlanAvailable = eventDto.IsPaymentPlanAvailable,
                                Information = eventDto.Information,
                                End = eventDto.EndTime,
                                Name = eventDto.SessionName,
                                Venue = new VenueResponseModel()
                                {
                                    Id = eventDto.VenueId,
                                    Name = eventDto.VenueName,
                                    Address = new AddressResponseModel()
                                    {
                                        Id = eventDto.AddressId,
                                        AddressLine1 = eventDto.AddressLine1,
                                        AddressLine2 = eventDto.AddressLine2,
                                        City = eventDto.City,
                                        Country = eventDto.Country,
                                        County = eventDto.County,
                                        Name = eventDto.AddressName,
                                        PostCode = eventDto.PostCode,
                                    }
                                },
                                PasswordEnabled = eventDto.SessionPasswordEnabled,
                                MD5String = eventDto.SessionMD5String
                            });

                            var eventImages = await con.QueryAsync<EventImageDto>(
                                 "SELECT * FROM public.fngeteventimages(@p_eventid)",
                                 new
                                 {
                                     @p_eventid = eventDto.EventId
                                 });

                            foreach (var img in eventImages)
                            {
                                eventModel.Images.Add(new ImageResponseModel
                                {
                                    Id = img.ImageId,
                                    Name = img.ImageName,
                                    Primary = img.PrimaryImage,
                                    Uri = img.ImageUri
                                });
                            }

                            currentEventId = eventDto.EventId;
                        }
                        else
                        {
                            eventModel.Sessions.Add(
                                new SessionResponseModel()
                                {
                                    Id = eventDto.SessionId,
                                    Start = eventDto.StartTime,
                                    Description = eventDto.SessionDescription,
                                    AdditionalInformation = eventDto.AdditionalInformation,
                                    IsSoldout = eventDto.IsSoldOut,
                                    MaxTickets = eventDto.MaxTickets,
                                    IsPaymentPlanAvailable = eventDto.IsPaymentPlanAvailable,
                                    Information = eventDto.Information,
                                    End = eventDto.EndTime,
                                    Name = eventDto.SessionName,
                                    Venue = new VenueResponseModel()
                                    {
                                        Id = eventDto.VenueId,
                                        Name = eventDto.VenueName,
                                        Address = new AddressResponseModel()
                                        {
                                            Id = eventDto.AddressId,
                                            AddressLine1 = eventDto.AddressLine1,
                                            AddressLine2 = eventDto.AddressLine2,
                                            City = eventDto.City,
                                            Country = eventDto.Country,
                                            County = eventDto.County,
                                            Name = eventDto.AddressName,
                                            PostCode = eventDto.PostCode,
                                        }
                                    },
                                    PasswordEnabled = eventDto.SessionPasswordEnabled,
                                    MD5String = eventDto.SessionMD5String
                                });
                        }
                    }
                }
                catch (Exception ex)
                {
                    con.Close();
                    Console.WriteLine("GetEventDapperAsync Error " + ex.ToString());
                    throw;
                }
                con.Close();
            }

            return eventModel;
        }

        public async Task<SessionResponseModel> GetSessionDapperAsync(long eventId, long sessionId)
        {
            var sessionModel = new SessionResponseModel();

            using (var con = new DataContext(configuration).CreateConnection())
            {
                try
                {
                    var sessionModelDto = await con.QueryAsync<SessionModelDto>(
                        "SELECT * FROM fngeteventsession(@p_eventid, @p_sessionid)",
                        new
                        {
                            p_eventid = eventId,
                            p_sessionid = sessionId
                        });

                    foreach (var session in sessionModelDto)
                    {
                        sessionModel = new SessionResponseModel()
                        {
                            Id = session.SessionId,
                            Start = session.StartTime,
                            Description = session.Description,
                            AdditionalInformation = session.AdditionalInformation,

                            IsSoldout = session.IsSoldOut,
                            MaxTickets = session.MaxTickets,
                            IsPaymentPlanAvailable = session.IsPaymentPlanAvailable,
                            Information = session.Information,
                            End = session.EndTime,
                            Name = session.SessionName,
                            Venue = new VenueResponseModel()
                            {
                                Id = session.VenueId,
                                Name = session.VenueName,
                                Address = new AddressResponseModel()
                                {
                                    Id = session.AddressId,
                                    AddressLine1 = session.AddressLine1,
                                    AddressLine2 = session.AddressLine2,
                                    City = session.City,
                                    Country = session.Country,
                                    County = session.County,
                                    Name = session.AddressName,
                                    PostCode = session.PostCode,
                                }
                            },
                            PasswordEnabled = session.PasswordEnabled,
                            MD5String = session.MD5String
                        };
                    }
                }
                catch (Exception ex)
                {
                    con.Close();
                    Console.WriteLine("GetSessionDapperAsync Error " + ex.ToString());
                    throw;
                }
                con.Close();
            }

            return sessionModel;
        }

        public async Task<IList<EventResponseModel>> GetEventsDapperAsync()
        {
            var eventModel = new List<EventResponseModel>();

            using (var con = new DataContext(configuration).CreateConnection())
            {
                try
                {
                    long currentEventId = -1;

                    var eventModelDto = await con.QueryAsync<EventModelDto>(
                        "SELECT * FROM public.fngetevents()");

                    foreach (var eventDto in eventModelDto)
                    {
                        if (currentEventId != eventDto.EventId)
                        {
                            var em = new EventResponseModel()
                            {
                                Description = eventDto.EventDescription,
                                Name = eventDto.EventName,
                                Id = eventDto.EventId,
                                Slug = eventDto.Slug,
                                PubliciseStart = eventDto.PubliciseStart,
                                PubliciseEnd = eventDto.PubliciseEnd,
                                Images = new List<ImageResponseModel>(),
                                Sessions = new List<SessionResponseModel>(),
                                PasswordEnabled = eventDto.PasswordEnabled,
                                MD5String = eventDto.MD5String
                            };
                            em.Sessions.Add(new SessionResponseModel()
                            {
                                Id = eventDto.SessionId,
                                Start = eventDto.StartTime,
                                Description = eventDto.SessionDescription,
                                AdditionalInformation = eventDto.AdditionalInformation,
                                IsSoldout = eventDto.IsSoldOut,
                                MaxTickets = eventDto.MaxTickets,
                                IsPaymentPlanAvailable = eventDto.IsPaymentPlanAvailable,
                                Information = eventDto.Information,
                                End = eventDto.EndTime,
                                Name = eventDto.SessionName,
                                Venue = new VenueResponseModel()
                                {
                                    Id = eventDto.VenueId,
                                    Name = eventDto.VenueName,
                                    Address = new AddressResponseModel()
                                    {
                                        Id = eventDto.AddressId,
                                        AddressLine1 = eventDto.AddressLine1,
                                        AddressLine2 = eventDto.AddressLine2,
                                        City = eventDto.City,
                                        Country = eventDto.Country,
                                        County = eventDto.County,
                                        Name = eventDto.AddressName,
                                        PostCode = eventDto.PostCode,
                                    }
                                },
                                PasswordEnabled = eventDto.SessionPasswordEnabled,
                                MD5String = eventDto.SessionMD5String
                            });

                            var eventImages = await con.QueryAsync<EventImageDto>(
                                 "SELECT * FROM public.fngeteventimages(@p_eventid)", new {
                                            @p_eventid = eventDto.EventId
                                        });

                            foreach (var img in eventImages)
                            {
                                em.Images.Add(new ImageResponseModel
                                {
                                    Id = img.ImageId,
                                    Name = img.ImageName,
                                    Primary = img.PrimaryImage,
                                    Uri = img.ImageUri
                                });
                            }

                            eventModel.Add(em);

                            currentEventId = eventDto.EventId;
                        }
                        else
                        {
                            eventModel.First(x => x.Id == eventDto.EventId).Sessions.Add(
                                new SessionResponseModel()
                                {
                                    Id = eventDto.SessionId,
                                    Start = eventDto.StartTime,
                                    Description = eventDto.SessionDescription,
                                    AdditionalInformation = eventDto.AdditionalInformation,
                                    IsSoldout = eventDto.IsSoldOut,
                                    MaxTickets = eventDto.MaxTickets,
                                    IsPaymentPlanAvailable = eventDto.IsPaymentPlanAvailable,
                                    Information = eventDto.Information,
                                    End = eventDto.EndTime,
                                    Name = eventDto.SessionName,
                                    Venue = new VenueResponseModel()
                                    {
                                        Id = eventDto.VenueId,
                                        Name = eventDto.VenueName,
                                        Address = new AddressResponseModel()
                                        {
                                            Id = eventDto.AddressId,
                                            AddressLine1 = eventDto.AddressLine1,
                                            AddressLine2 = eventDto.AddressLine2,
                                            City = eventDto.City,
                                            Country = eventDto.Country,
                                            County = eventDto.County,
                                            Name = eventDto.AddressName,
                                            PostCode = eventDto.PostCode,
                                        }
                                    },
                                    PasswordEnabled = eventDto.SessionPasswordEnabled,
                                    MD5String = eventDto.SessionMD5String
                                });
                        }
                    }
                }
                catch (Exception ex)
                {
                    con.Close();
                    Console.WriteLine("GetEventDapperAsync Error " + ex.ToString());
                    throw;
                }
                con.Close();
            }

            return eventModel;
        }
    }
}