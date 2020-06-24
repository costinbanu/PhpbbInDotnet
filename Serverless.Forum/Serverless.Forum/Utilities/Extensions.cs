﻿using Microsoft.AspNetCore.Http;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Serverless.Forum.Contracts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Serverless.Forum.Utilities
{
    public static class Extensions
    {
        static readonly DateTime DATE_SEED = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public static DateTime ToUtcTime(this long timestamp)
            => DATE_SEED.AddSeconds(timestamp);

        public static long ToUnixTimestamp(this DateTime time)
            => (long)time.ToUniversalTime().Subtract(DATE_SEED).TotalSeconds;

        public static bool IsMimeTypeInline(this string mimeType)
            => mimeType.StartsWith("image", StringComparison.InvariantCultureIgnoreCase) ||
                mimeType.StartsWith("video", StringComparison.InvariantCultureIgnoreCase)/* ||
                mimeType.EndsWith("pdf", StringComparison.InvariantCultureIgnoreCase)*/;

        public static void RunSync(this Task asyncTask)
            => asyncTask.GetAwaiter().GetResult();

        public static T RunSync<T>(this Task<T> asyncTask)
            => asyncTask.GetAwaiter().GetResult();

        public static Bitmap ToImage(this IFormFile file)
        {
            if (file == null)
            {
                return null;
            }
            try
            {
                var stream = file.OpenReadStream();
                using var bmp = new Bitmap(stream);
                stream.Seek(0, SeekOrigin.Begin);
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public static async Task OpenIfNeeded(this DbConnection connection)
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }
        }

        //public static async Task<T> FirstOrDefaultAsync<T>(this DbSet<T> source) where T : class
        //{
        //    return await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(source);
        //}

        public static async Task<T> FirstOrDefaultAsync<T>(this IQueryable<T> source) where T : class
        {
            return await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(source);
        }

        //public static async Task<T> FirstOrDefaultAsync<T>(this DbSet<T> source, Expression<Func<T, bool>> filter) where T : class
        //{
        //    return await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(source, filter);
        //}

        public static async Task<T> FirstOrDefaultAsync<T>(this IQueryable<T> source, Expression<Func<T, bool>> filter) where T : class
        {
            return await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(source, filter);
        }

        public static IQueryable<T> AsNoTracking<T>(this DbSet<T> source) where T : class
        {
            return EntityFrameworkQueryableExtensions.AsNoTracking(source);
        }

        //public static async Task<int> CountAsync<T>(this DbSet<T> source, Expression<Func<T, bool>> filter) where T : class
        //{
        //    return await EntityFrameworkQueryableExtensions.CountAsync(source, filter);
        //}

        //public static async Task<int> CountAsync<T>(this DbSet<T> source) where T : class
        //{
        //    return await EntityFrameworkQueryableExtensions.CountAsync(source);
        //}

        public static async Task<int> CountAsync<T>(this IQueryable<T> source, Expression<Func<T, bool>> filter) where T : class
        {
            return await EntityFrameworkQueryableExtensions.CountAsync<T>(source, filter);
        }

        public static async Task<int> CountAsync<T>(this IQueryable<T> source) where T : class
        {
            return await EntityFrameworkQueryableExtensions.CountAsync<T>(source);
        }

        public static async Task<T2> MaxAsync<T1, T2>(this IQueryable<T1> source, Expression<Func<T1, T2>> filter) where T2 : IComparable
        {
            return await EntityFrameworkQueryableExtensions.MaxAsync(source, filter);
        }

        public static IQueryable<T> Where<T>(this DbSet<T> source, Expression<Func<T, bool>> filter) where T : class
        {
            return Queryable.Where<T>(source, filter);
        }

        public static async Task<List<T>> ToListAsync<T>(this IQueryable<T> source)
        {
            return await EntityFrameworkQueryableExtensions.ToListAsync<T>(source);
        }

        public static ForumDto ToForumDto(this ForumTree tree)
            => new ForumDto
            {
                Id = tree.ForumId,
                Name = tree.ForumName,
                Description = tree.ForumDesc,
                DescriptionBbCodeUid = tree.ForumDescUid,
                ForumPassword = tree.ForumPassword,
                ForumType = tree.ForumType
            }
    }
}
