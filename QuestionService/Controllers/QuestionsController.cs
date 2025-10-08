using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.DTOs;
using QuestionService.Models;
using System.Security.Claims;

namespace QuestionService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class QuestionsController(QuestionDbContext db) : ControllerBase
    {
        [HttpPost]
        [Authorize]
        public async Task<ActionResult<Question>> CreateQuestion(CreateQuestionDto dto)
        {
            var validTags = await db.Tags.Where(t => dto.Tags.Contains(t.Slug)).ToListAsync();

            var missing = dto.Tags.Except(validTags.Select(t => t.Slug).ToList()).ToList();

            if (missing.Count != 0)
            {
                return BadRequest($"Invalid tags: {string.Join(", ", missing)}");
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue("name");
            if (userId is null || name is null) return BadRequest("Cannot get user details");
            var question = new Question
            {
                Title = dto.Title,
                Content = dto.Content,
                TagSlugs = dto.Tags,
                AskerId = userId,
                AskerDisplayName = name,
            };
            db.Questions.Add(question);
            await db.SaveChangesAsync();
            return Created($"/questions/{question.Id}", question);
        }


        [HttpGet]
        public async Task<ActionResult<List<Question>>> GetQuestions(string? tag)
        {
            var query = db.Questions.AsQueryable();
            if (!string.IsNullOrEmpty(tag))
            {
                query = query.Where(t => t.TagSlugs.Contains(tag));
            }
            return await query.OrderByDescending(q => q.CreatedAt).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Question>> GetQuestion(string id)
        {
            var question = await db.Questions.FindAsync(id);
            if (question is null) return NotFound();

            // fire and forget update of view counts
            await db.Questions.Where(q => q.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(p => p.ViewCount, p => p.ViewCount + 1));

            return question;

        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<ActionResult> UpdateQuestion(string id, CreateQuestionDto dto)
        {
            var question = await db.Questions.FindAsync(id);
            if (question is null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != question.AskerId) return Forbid();

            var validTags = await db.Tags.Where(t => dto.Tags.Contains(t.Slug)).ToListAsync();

            var missing = dto.Tags.Except(validTags.Select(t => t.Slug).ToList()).ToList();

            if (missing.Count != 0)
            {
                return BadRequest($"Invalid tags: {string.Join(", ", missing)}");
            }


            question.Title = dto.Title;
            question.Content = dto.Content;
            question.TagSlugs = dto.Tags;
            question.UpdatedAt = DateTime.UtcNow;

            await db.SaveChangesAsync();
            return NoContent();

        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteQuestion(string id)
        {
            var question = await db.Questions.FindAsync(id);
            if (question is null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != question.AskerId) return Forbid();

            db.Questions.Remove(question);
            await db.SaveChangesAsync();
            return NoContent();
        }


    }
}
