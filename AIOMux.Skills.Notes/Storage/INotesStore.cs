using AIOMux.Core.Interfaces;
using AIOMux.Skills.Notes.Models;

namespace AIOMux.Skills.Notes.Storage;

/// <summary>
/// Tool interface for notes storage operations.
/// This is the ONLY tool that NotesSkill is allowed to use.
/// </summary>
public interface INotesStore : ITool
{
    /// <summary>
    /// Save a note record to storage.
    /// </summary>
    Task<string> SaveAsync(NoteRecord note);

    /// <summary>
    /// Get a note by ID.
    /// </summary>
    Task<NoteRecord?> GetAsync(string id);

    /// <summary>
    /// Search notes based on query parameters.
    /// </summary>
    Task<List<NoteRecord>> SearchAsync(NoteQuery query);

    /// <summary>
    /// Get all notes.
    /// </summary>
    Task<List<NoteRecord>> GetAllAsync();
}
