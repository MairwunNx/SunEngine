﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AngleSharp.Dom.Html;
using AngleSharp.Parser.Html;
using SunEngine.Core.Models;
using SunEngine.Core.Models.Materials;
using SunEngine.Core.Utils;
using SunEngine.Core.Utils.TextProcess;

namespace SunEngine.DataSeed
{
    /// <summary>
    /// Seed materials and comments to DataContainer for testing purposes 
    /// </summary>
    public class MaterialsSeeder
    {
        public struct LinesCount
        {
            public int Min;
            public int Max;
        }

        private readonly DataContainer dataContainer;

        private readonly Random ran = new Random();

        public int MinMaterialCount = 5;
        public int MaxMaterialCount = 20;
        public int CommentsCount = 12;
        public bool TitleAppendCategoryName;

        private const int MaterialSubTitleLength = 80;
        private const int MaterialPreviewLength = 800;


        private readonly LinesCount defaultLinesCount = new LinesCount {Min = 4, Max = 30};

        
        private readonly Dictionary<string, Func<IHtmlDocument, int, string>> MaterialPreviewGenerators =
            new Dictionary<string, Func<IHtmlDocument, int, string>>
            {
                [nameof(MakePreview.PlainText)] = MakePreview.PlainText,
                [nameof(MakePreview.HtmlFirstImage)] = MakePreview.HtmlFirstImage,
                [nameof(MakePreview.HtmlNoImages)] = MakePreview.HtmlNoImages
            };


        public MaterialsSeeder(DataContainer dataContainer)
        {
            this.dataContainer = dataContainer;
        }

        /*public void Seed()
        {
            foreach (var category in dataContainer.Categories.Where(x => x.IsMaterialsContainer))
            {
                SeedCategoryWithMaterials(category, category.MaterialTypeTitle, TitleAppendCategoryName);
            }
        }*/

        public void SeedCategoryAndSub(string categoryName)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Seed test materials and comments in memory");
            Console.ResetColor();

            SeedCategoryAndSubRec(categoryName);
        }

        public void SeedCategoryAndSubRec(string categoryName)
        {
            var category =
                dataContainer.Categories.FirstOrDefault(x => x.NameNormalized == Normalizer.Normalize(categoryName));
            if (category == null)
                throw new Exception($"No category '{categoryName}' in data base");

            MaterialPreviewGenerators.TryGetValue(category.MaterialsPreviewGeneratorName ?? "",
                out Func<IHtmlDocument, int, string> generator);


            if (category.IsMaterialsContainer)
                SeedCategoryWithMaterials(category, generator, category.MaterialTypeTitle, TitleAppendCategoryName);

            foreach (var subCategory in dataContainer.Categories.Where(x =>
                x.ParentId.HasValue && x.ParentId.Value == category.Id))
                SeedCategoryAndSubRec(subCategory.Name);
        }

        public void SeedCategoryWithMaterials(
            Category category, Func<IHtmlDocument, int, string> previewGenerator, string titleStart = null,
            bool titleAppendCategoryName = false,
            int? materialsCount = null, LinesCount? linesCount = null)
        {
            if (materialsCount == null)
                materialsCount = ran.Next(MinMaterialCount, MaxMaterialCount);
            if (materialsCount == 0)
                return;


            Console.WriteLine($"'{category.Name}' category with {materialsCount} mat, {CommentsCount} comm");

            if (linesCount == null)
                linesCount = defaultLinesCount;

            for (int i = 1; i <= materialsCount; i++)
            {
                var title = titleStart != null ? titleStart + " " + i : $"Материал {i}";
                if (titleAppendCategoryName)
                    title += $" ({category.Name})";

                SeedMaterial(category, previewGenerator, title, CommentsCount,
                    $"{titleStart ?? "Материал"} {i}, категория {category.Name}", "материал " + i,
                    linesCount.Value);
            }
        }

        public Material SeedMaterial(
            Category category, Func<IHtmlDocument, int, string> previewGenerator, string title, int commentsCount,
            string firstLine,
            string lineElement, LinesCount linesCount)
        {
            var publishDate = dataContainer.IterateCommentPublishDate();
            int linesCountCurrent = ran.Next(linesCount.Min, linesCount.Max);

            int id = dataContainer.NextMaterialId();

            Material material = new Material
            {
                Id = id,
                Title = title,
                Text = MakeSeedText(lineElement, 8, linesCountCurrent, firstLine),
                AuthorId = dataContainer.GetRandomUserId(),
                CategoryId = category.Id,
                PublishDate = publishDate,
                LastActivity = publishDate,
                SortNumber = id
            };

            IHtmlDocument doc = null;
            if (previewGenerator != null)
            {
                doc = new HtmlParser().Parse(material.Text);
                material.Preview = previewGenerator(doc, MaterialPreviewLength);
            }


            switch (category.MaterialsSubTitleInputType)
            {
                case MaterialsSubTitleInputType.Manual:
                    material.SubTitle = "Описание материала: " + material.Title;
                    break;
                case MaterialsSubTitleInputType.Auto:
                    material.SubTitle = MakeSubTitle.Do(doc ?? new HtmlParser().Parse(material.Text),
                        MaterialSubTitleLength);
                    break;
            }


            if (commentsCount > 0)
            {
                IList<Comment> comments = MakeComments(material, commentsCount);

                //Comment last = comments.OrderByDescending(x=>x.PublishDate).First();
                //material.SetLastComment(last);

                material.LastActivity = comments.OrderByDescending(x => x.PublishDate).First().PublishDate;
                material.CommentsCount = comments.Count;

                dataContainer.Comments.AddRange(comments);
            }

            dataContainer.Materials.Add(material);

            return material;
        }

        public IList<Comment> MakeComments(Material material, int commentsCount)
        {
            List<Comment> addedComments = new List<Comment>();

            for (int i = 1; i <= CommentsCount; i++)
            {
                Comment comment = new Comment
                {
                    Id = dataContainer.NextCommentId(),
                    Text = "",
                    PublishDate = dataContainer.IterateCommentPublishDate(),
                    MaterialId = material.Id,
                    AuthorId = dataContainer.GetRandomUserId()
                };

                dataContainer.IterateCommentPublishDate();

                comment.Text = MakeSeedText("комментарий " + i, 8, 4,
                    $"Комментарий: {comment.Id}, материал {material.Id}");

                addedComments.Add(comment);
            }

            return addedComments;
        }


        private string MakeSeedText(string str, int wordInLine, int lines, string firstLine = null)
        {
            StringBuilder sb = new StringBuilder();
            if (firstLine != null)
                sb.AppendLine($"<p>{firstLine}</p>");

            for (int i = 0; i < lines; i += 3)
            {
                sb.Append("<p>");

                for (int j = 0; j < 3; j++)
                {
                    for (int k = 0; k < wordInLine; k++)
                        sb.Append(str + " ");

                    if (j != 2)
                        sb.Append("<br/>");
                }

                sb.Append("</p>\n");
            }

            return sb.ToString();
        }
    }


    public static class MaterialExtension
    {
        public static void SetLastComment(this Material material, Comment comment)
        {
            if (comment != null)
            {
                material.LastCommentId = comment.Id;
                material.LastActivity = comment.PublishDate;
            }
        }
    }
}
